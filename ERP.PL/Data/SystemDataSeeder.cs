using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.PL.Data
{
    /// <summary>
    /// Resets and seeds all modules with rich test data for manual QA.
    /// </summary>
    public static class SystemDataSeeder
    {
        private const string DefaultPassword = "Test@12345Ab";
        private const string SeedVersion = "1.0.0";

        public static Task SeedDevelopmentAsync(
                                                ApplicationDbContext context,
                                                UserManager<ApplicationUser> userManager,
                                                RoleManager<IdentityRole> roleManager,
                                                ILogger logger,
                                                bool resetDatabase = false)
        {
            return SeedAsync(context, userManager, roleManager, logger, "Development", resetDatabase);
        }

        public static async Task SeedProductionAsync(
                                                    ApplicationDbContext context,
                                                    UserManager<ApplicationUser> userManager,
                                                    RoleManager<IdentityRole> roleManager,
                                                    IConfiguration configuration,
                                                    ILogger logger)
        {
            logger.LogInformation("Starting production baseline seed operation.");

            await EnsureDatabaseReadyAsync(context, logger);

            // Check if already seeded
            if (await IsSeedCompletedAsync(context, "Production", SeedVersion))
            {
                logger.LogInformation("Production seed version {Version} already completed. Skipping.", SeedVersion);
                return;
            }

            await SeedRolesAsync(roleManager);

            var adminEmail = configuration["Seed:ProductionAdminEmail"];
            var adminPassword = configuration["Seed:ProductionAdminPassword"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning("Production baseline seeding skipped admin user creation. Set Seed:ProductionAdminEmail and Seed:ProductionAdminPassword to enable.");
                return;
            }

            var user = await userManager.FindByEmailAsync(adminEmail);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    RequirePasswordChange = true
                };

                var createResult = await userManager.CreateAsync(user, adminPassword);
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException($"Failed creating production admin user {adminEmail}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                }
            }

            await EnsureUserInRolesAsync(userManager, user, "CEO", "ITAdmin");

            // Mark seed as completed
            await MarkSeedCompletedAsync(context, "Production", SeedVersion, $"Production admin created: {adminEmail}");


            logger.LogInformation("Production baseline seeding completed for {Email}.", adminEmail);
        }

        public static async Task SeedAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger logger, string environment, bool resetDatabase = false)
        {
            try
            {
                logger.LogInformation("Starting system seed operation. Environment: {Environment}, Reset enabled: {ResetDatabase}", environment, resetDatabase);


                if (resetDatabase)
                {
                    if (string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning("Skipping destructive database reset in Testing environment.");
                    }
                    else
                    {
                        await context.Database.EnsureDeletedAsync();
                        logger.LogWarning("Database deleted for reset.");
                    }
                }

                await EnsureDatabaseReadyAsync(context, logger);
                // Check if seeding already completed for this version
                if (!resetDatabase && await IsSeedCompletedAsync(context, environment, SeedVersion))
                {
                    logger.LogInformation("Seed version {Version} for {Environment} already completed. Skipping full seed.", SeedVersion, environment);
                    return;
                }

                await SeedRolesAsync(roleManager);

                var now = DateTime.UtcNow;

                var employees = await SeedEmployeesAsync(context, now, logger);
                var departments = await SeedDepartmentsAsync(context, employees, now, logger);
                await AssignEmployeesToDepartmentsAsync(context, employees, departments, logger);
                var projects = await SeedProjectsAsync(context, employees, departments, now, logger);
                await SeedProjectAssignmentsAsync(context, projects, employees, logger);

                var users = await SeedUsersAsync(userManager, context, employees, now, logger);
                await SeedTasksAndCommentsAsync(context, projects, employees, users, now, logger);
                await SeedAuditAndResetDataAsync(context, users, now, logger);
                // Mark seed as completed
                await MarkSeedCompletedAsync(context, environment, SeedVersion, "Full system data seeded successfully");


                logger.LogWarning("System data seeding completed. Default password for seeded users: {Password}", DefaultPassword);
            }
            catch (Exception ex)
            {

                // Mark seed as failed
                await MarkSeedFailedAsync(context, environment, SeedVersion, ex.Message);
                throw;
            }
        }

        private static async Task<bool> IsSeedCompletedAsync(ApplicationDbContext context, string environment, string version)
        {
            return await context.SeedHistories
                .AnyAsync(s => s.SeedVersion == version
                            && s.Environment == environment
                            && s.IsSuccessful);
        }

        private static async Task MarkSeedCompletedAsync(ApplicationDbContext context, string environment, string version, string notes)
        {
            context.SeedHistories.Add(new SeedHistory
            {
                SeedVersion = version,
                Environment = environment,
                SeededAt = DateTime.UtcNow,
                IsSuccessful = true,
                Notes = notes
            });

            await context.SaveChangesAsync();
        }

        private static async Task MarkSeedFailedAsync(ApplicationDbContext context, string environment, string version, string errorMessage)
        {
            try
            {
                context.SeedHistories.Add(new SeedHistory
                {
                    SeedVersion = version,
                    Environment = environment,
                    SeededAt = DateTime.UtcNow,
                    IsSuccessful = false,
                    Notes = $"Failed: {errorMessage}"
                });

                await context.SaveChangesAsync();
            }
            catch
            {
                // Ignore if we can't save the failure record
            }
        }


        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            var roles = new[] { "CEO", "ITAdmin", "DepartmentManager", "ProjectManager", "Employee" };

            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException($"Failed creating role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
            }
        }

        private static async Task<Dictionary<string, Employee>> SeedEmployeesAsync(ApplicationDbContext context, DateTime now, ILogger logger)
        {
            // Check if employees already exist
            if (await context.Employees.AnyAsync())
            {
                logger.LogInformation("Employees already exist. Loading existing data.");
                var existingEmployees = await context.Employees
                    .Where(e => !e.IsDeleted)
                    .ToListAsync();
                return existingEmployees.ToDictionary(e => e.Email, StringComparer.OrdinalIgnoreCase);
            }

            logger.LogInformation("Seeding employees...");

            var employees = new List<Employee>
            {
                new() { FirstName = "Sarah", LastName = "Ahmed", Email = "sarah.ahmed@company.com", PhoneNumber = "201000000001", Position = "HR Director", HireDate = now.AddYears(-6), Salary = 210000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-female.png", Gender = Gender.Female },
                new() { FirstName = "Omar", LastName = "Hassan", Email = "omar.hassan@company.com", PhoneNumber = "201000000002", Position = "Engineering Director", HireDate = now.AddYears(-7), Salary = 260000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-male.png", Gender = Gender.Male },
                new() { FirstName = "Laila", LastName = "Nasser", Email = "laila.nasser@company.com", PhoneNumber = "201000000003", Position = "Finance Manager", HireDate = now.AddYears(-5), Salary = 190000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-female.png", Gender = Gender.Female },
                new() { FirstName = "Kareem", LastName = "Youssef", Email = "kareem.youssef@company.com", PhoneNumber = "201000000004", Position = "Operations Manager", HireDate = now.AddYears(-4), Salary = 180000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-male.png", Gender = Gender.Male },

                new() { FirstName = "Mona", LastName = "Farouk", Email = "mona.farouk@company.com", PhoneNumber = "201000000005", Position = "Senior Project Manager", HireDate = now.AddYears(-5), Salary = 220000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-female.png", Gender = Gender.Female },
                new() { FirstName = "Yassin", LastName = "Ali", Email = "yassin.ali@company.com", PhoneNumber = "201000000006", Position = "Project Manager", HireDate = now.AddYears(-4), Salary = 205000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-male.png", Gender = Gender.Male },

                new() { FirstName = "Nada", LastName = "Mostafa", Email = "nada.mostafa@company.com", PhoneNumber = "201000000007", Position = "Backend Engineer", HireDate = now.AddYears(-3), Salary = 125000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-female.png", Gender = Gender.Female },
                new() { FirstName = "Hany", LastName = "Saad", Email = "hany.saad@company.com", PhoneNumber = "201000000008", Position = "Frontend Engineer", HireDate = now.AddYears(-2), Salary = 115000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-male.png", Gender = Gender.Male },
                new() { FirstName = "Reem", LastName = "Gamal", Email = "reem.gamal@company.com", PhoneNumber = "201000000009", Position = "QA Engineer", HireDate = now.AddYears(-2), Salary = 98000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-female.png", Gender = Gender.Female },
                new() { FirstName = "Tamer", LastName = "Fathy", Email = "tamer.fathy@company.com", PhoneNumber = "201000000010", Position = "DevOps Engineer", HireDate = now.AddYears(-3), Salary = 132000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-male.png", Gender = Gender.Male },
                new() { FirstName = "Dina", LastName = "Adel", Email = "dina.adel@company.com", PhoneNumber = "201000000011", Position = "HR Specialist", HireDate = now.AddYears(-2), Salary = 87000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-female.png", Gender = Gender.Female },
                new() { FirstName = "Fady", LastName = "Ibrahim", Email = "fady.ibrahim@company.com", PhoneNumber = "201000000012", Position = "Accountant", HireDate = now.AddYears(-1), Salary = 84000, IsActive = true, IsDeleted = false, CreatedAt = now, ImageUrl = "/uploads/images/avatar-male.png", Gender = Gender.Male }
            };

            context.Employees.AddRange(employees);
            await context.SaveChangesAsync();

            return employees.ToDictionary(e => e.Email, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, Department>> SeedDepartmentsAsync(ApplicationDbContext context, Dictionary<string, Employee> employees, DateTime now, ILogger logger)
        {
            // Check if departments already exist
            if (await context.Departments.AnyAsync())
            {
                logger.LogInformation("Departments already exist. Loading existing data.");
                var existingDepartments = await context.Departments
                    .Where(d => !d.IsDeleted)
                    .ToListAsync();
                return existingDepartments.ToDictionary(d => d.DepartmentCode, StringComparer.OrdinalIgnoreCase);
            }

            logger.LogInformation("Seeding departments...");


            var departments = new List<Department>
            {
                new() { DepartmentCode = "HRD_101", DepartmentName = "Human Resources", ManagerId = employees["sarah.ahmed@company.com"].Id, CreatedAt = now, IsDeleted = false },
                new() { DepartmentCode = "ENG_102", DepartmentName = "Engineering", ManagerId = employees["omar.hassan@company.com"].Id, CreatedAt = now, IsDeleted = false },
                new() { DepartmentCode = "FIN_103", DepartmentName = "Finance", ManagerId = employees["laila.nasser@company.com"].Id, CreatedAt = now, IsDeleted = false },
                new() { DepartmentCode = "OPS_104", DepartmentName = "Operations", ManagerId = employees["kareem.youssef@company.com"].Id, CreatedAt = now, IsDeleted = false }
            };

            context.Departments.AddRange(departments);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} employees.", employees.Count);
            logger.LogInformation("Seeded {Count} departments.", departments.Count);


            return departments.ToDictionary(d => d.DepartmentCode, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task AssignEmployeesToDepartmentsAsync(ApplicationDbContext context, Dictionary<string, Employee> employees, Dictionary<string, Department> departments, ILogger logger)
        {
            // Check if assignments already exist
            if (employees.Values.Any(e => e.DepartmentId != null))
            {
                logger.LogInformation("Employee department assignments already exist. Skipping.");
                return;
            }

            logger.LogInformation("Assigning employees to departments...");


            employees["sarah.ahmed@company.com"].DepartmentId = departments["HRD_101"].Id;
            employees["dina.adel@company.com"].DepartmentId = departments["HRD_101"].Id;

            employees["omar.hassan@company.com"].DepartmentId = departments["ENG_102"].Id;
            employees["mona.farouk@company.com"].DepartmentId = departments["ENG_102"].Id;
            employees["yassin.ali@company.com"].DepartmentId = departments["ENG_102"].Id;
            employees["nada.mostafa@company.com"].DepartmentId = departments["ENG_102"].Id;
            employees["hany.saad@company.com"].DepartmentId = departments["ENG_102"].Id;
            employees["reem.gamal@company.com"].DepartmentId = departments["ENG_102"].Id;
            employees["tamer.fathy@company.com"].DepartmentId = departments["ENG_102"].Id;

            employees["laila.nasser@company.com"].DepartmentId = departments["FIN_103"].Id;
            employees["fady.ibrahim@company.com"].DepartmentId = departments["FIN_103"].Id;

            employees["kareem.youssef@company.com"].DepartmentId = departments["OPS_104"].Id;

            await context.SaveChangesAsync();
            logger.LogInformation("Department assignments completed.");

        }

        private static async Task<Dictionary<string, Project>> SeedProjectsAsync(ApplicationDbContext context, Dictionary<string, Employee> employees, Dictionary<string, Department> departments, DateTime now, ILogger logger)
        {
            // Check if projects already exist
            if (await context.Projects.AnyAsync())
            {
                logger.LogInformation("Projects already exist. Loading existing data.");
                var existingProjects = await context.Projects
                    .Where(p => !p.IsDeleted)
                    .ToListAsync();
                return existingProjects.ToDictionary(p => p.ProjectCode, StringComparer.OrdinalIgnoreCase);
            }

            logger.LogInformation("Seeding projects...");


            var projects = new List<Project>
            {
                new()
                {
                    ProjectCode = "PRJ-2026-001",
                    ProjectName = "Corporate ERP Modernization",
                    Description = "Modernize HR, Finance, and Operations modules with cross-department automation.",
                    DepartmentId = departments["ENG_102"].Id,
                    ProjectManagerId = employees["mona.farouk@company.com"].Id,
                    StartDate = now.AddMonths(-4),
                    EndDate = now.AddMonths(5),
                    Budget = 1500000,
                    Status = ProjectStatus.InProgress,
                    CreatedAt = now,
                    IsDeleted = false
                },
                new()
                {
                    ProjectCode = "PRJ-2026-002",
                    ProjectName = "Field Operations Mobility",
                    Description = "Deploy mobile workflows and approvals for operations and finance teams.",
                    DepartmentId = departments["OPS_104"].Id,
                    ProjectManagerId = employees["yassin.ali@company.com"].Id,
                    StartDate = now.AddMonths(-2),
                    EndDate = now.AddMonths(8),
                    Budget = 920000,
                    Status = ProjectStatus.Planning,
                    CreatedAt = now,
                    IsDeleted = false
                },
                new()
                {
                    ProjectCode = "PRJ-2026-003",
                    ProjectName = "Talent Analytics Platform",
                    Description = "Build analytics dashboards for hiring, retention, and workforce planning.",
                    DepartmentId = departments["HRD_101"].Id,
                    StartDate = now.AddMonths(-1),
                    EndDate = now.AddMonths(6),
                    Budget = 640000,
                    Status = ProjectStatus.OnHold,
                    CreatedAt = now,
                    IsDeleted = false
                }
            };

            context.Projects.AddRange(projects);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} projects.", projects.Count);
            return projects.ToDictionary(p => p.ProjectCode, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task SeedProjectAssignmentsAsync(ApplicationDbContext context, Dictionary<string, Project> projects, Dictionary<string, Employee> employees, ILogger logger)
        {
            // Check if project assignments already exist
            if (await context.ProjectEmployees.AnyAsync())
            {
                logger.LogInformation("Project assignments already exist. Skipping.");
                return;
            }

            logger.LogInformation("Seeding project assignments...");


            var alpha = projects["PRJ-2026-001"];
            var beta = projects["PRJ-2026-002"];

            var assignments = new List<(string Email, Project Project)>
            {
                ("mona.farouk@company.com", alpha),
                ("nada.mostafa@company.com", alpha),
                ("hany.saad@company.com", alpha),
                ("reem.gamal@company.com", alpha),
                ("tamer.fathy@company.com", alpha),

                ("yassin.ali@company.com", beta),
                ("kareem.youssef@company.com", beta),
                ("fady.ibrahim@company.com", beta)
            };

            foreach (var (email, project) in assignments)
            {
                var employee = employees[email];
                employee.ProjectId = project.Id;

                context.ProjectEmployees.Add(new ProjectEmployee
                {
                    ProjectId = project.Id,
                    EmployeeId = employee.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = "seed-system"
                });
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Project assignments completed.");

        }

        private static async Task<Dictionary<string, ApplicationUser>> SeedUsersAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            Dictionary<string, Employee> employees,
            DateTime now,
            ILogger logger)
        {
            logger.LogInformation("Seeding users and roles...");

            var users = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);

            var ceo = await CreateUserWithRolesAsync(userManager, "ceo@company.com", null, now, "CEO");
            var itAdmin = await CreateUserWithRolesAsync(userManager, "it.admin@company.com", null, now, "ITAdmin");

            users[ceo.Email!] = ceo;
            users[itAdmin.Email!] = itAdmin;

            var employeeRoleMap = new Dictionary<string, string[]>
            {
                ["sarah.ahmed@company.com"] = ["Employee", "DepartmentManager"],
                ["omar.hassan@company.com"] = ["Employee", "DepartmentManager"],
                ["laila.nasser@company.com"] = ["Employee", "DepartmentManager"],
                ["kareem.youssef@company.com"] = ["Employee", "DepartmentManager"],
                ["mona.farouk@company.com"] = ["Employee", "ProjectManager"],
                ["yassin.ali@company.com"] = ["Employee", "ProjectManager"],
                ["nada.mostafa@company.com"] = ["Employee"],
                ["hany.saad@company.com"] = ["Employee"],
                ["reem.gamal@company.com"] = ["Employee"],
                ["tamer.fathy@company.com"] = ["Employee"],
                ["dina.adel@company.com"] = ["Employee"],
                ["fady.ibrahim@company.com"] = ["Employee"]
            };

            foreach (var (email, roles) in employeeRoleMap)
            {
                var employee = employees[email];
                var user = await CreateUserWithRolesAsync(userManager, email, employee.Id, now, roles);
                users[email] = user;

                employee.ApplicationUserId = user.Id;
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} users.", users.Count);


            return users;
        }

        private static async Task<ApplicationUser> CreateUserWithRolesAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            int? employeeId,
            DateTime now,
            params string[] roles)
        {
            // Check if user already exists
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                return existingUser;
            }


            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = now,
                RequirePasswordChange = false,
                EmployeeId = employeeId
            };

            var createResult = await userManager.CreateAsync(user, DefaultPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed creating user {email}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }

            if (roles.Length > 0)
            {
                var roleResult = await userManager.AddToRolesAsync(user, roles);
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException($"Failed assigning roles for {email}: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                }
            }

            return user;
        }

        private static async Task SeedTasksAndCommentsAsync(
            ApplicationDbContext context,
            Dictionary<string, Project> projects,
            Dictionary<string, Employee> employees,
            Dictionary<string, ApplicationUser> users,
            DateTime now, ILogger logger)
        {
            // Check if tasks already exist
            if (await context.TaskItems.AnyAsync())
            {
                logger.LogInformation("Tasks already exist. Skipping.");
                return;
            }

            logger.LogInformation("Seeding tasks and comments...");

            var tasks = new List<TaskItem>
            {
                new()
                {
                    Title = "Finalize integration architecture",
                    Description = "Publish approved architecture for ERP modernization with data flow diagrams.",
                    ProjectId = projects["PRJ-2026-001"].Id,
                    AssignedToEmployeeId = employees["nada.mostafa@company.com"].Id,
                    CreatedByUserId = users["mona.farouk@company.com"].Id,
                    Priority = TaskPriority.Critical,
                    Status = TaskStatus.InProgress,
                    StartDate = now.AddDays(-15),
                    DueDate = now.AddDays(7),
                    EstimatedHours = 40,
                    ActualHours = 18,
                    CreatedAt = now.AddDays(-16),
                    UpdatedAt = now.AddDays(-1)
                },
                new()
                {
                    Title = "Implement employee profile UI refresh",
                    Description = "Upgrade employee profile UX and validation flows.",
                    ProjectId = projects["PRJ-2026-001"].Id,
                    AssignedToEmployeeId = employees["hany.saad@company.com"].Id,
                    CreatedByUserId = users["mona.farouk@company.com"].Id,
                    Priority = TaskPriority.High,
                    Status = TaskStatus.New,
                    DueDate = now.AddDays(10),
                    EstimatedHours = 24,
                    ActualHours = 0,
                    CreatedAt = now.AddDays(-5),
                    UpdatedAt = now.AddDays(-3)
                },
                new()
                {
                    Title = "Create regression test pack",
                    Description = "Prepare full manual/automation regression checklist for departments and projects.",
                    ProjectId = projects["PRJ-2026-001"].Id,
                    AssignedToEmployeeId = employees["reem.gamal@company.com"].Id,
                    CreatedByUserId = users["ceo@company.com"].Id,
                    Priority = TaskPriority.Medium,
                    Status = TaskStatus.Blocked,
                    DueDate = now.AddDays(14),
                    EstimatedHours = 36,
                    ActualHours = 12,
                    CreatedAt = now.AddDays(-8),
                    UpdatedAt = now.AddDays(-1)
                },
                new()
                {
                    Title = "Setup CI/CD hardening",
                    Description = "Apply branch protections and deployment health checks.",
                    ProjectId = projects["PRJ-2026-001"].Id,
                    AssignedToEmployeeId = employees["tamer.fathy@company.com"].Id,
                    CreatedByUserId = users["mona.farouk@company.com"].Id,
                    Priority = TaskPriority.High,
                    Status = TaskStatus.Completed,
                    StartDate = now.AddDays(-20),
                    DueDate = now.AddDays(-5),
                    CompletedAt = now.AddDays(-4),
                    EstimatedHours = 30,
                    ActualHours = 28,
                    CreatedAt = now.AddDays(-21),
                    UpdatedAt = now.AddDays(-4)
                },
                new()
                {
                    Title = "Design operations mobile forms",
                    Description = "Draft and validate mobile approval forms for field requests.",
                    ProjectId = projects["PRJ-2026-002"].Id,
                    AssignedToEmployeeId = employees["kareem.youssef@company.com"].Id,
                    CreatedByUserId = users["yassin.ali@company.com"].Id,
                    Priority = TaskPriority.Medium,
                    Status = TaskStatus.InProgress,
                    StartDate = now.AddDays(-9),
                    DueDate = now.AddDays(12),
                    EstimatedHours = 20,
                    ActualHours = 7,
                    CreatedAt = now.AddDays(-10),
                    UpdatedAt = now.AddDays(-2)
                },
                new()
                {
                    Title = "Prepare cost control baseline",
                    Description = "Set quarterly budget checkpoints and reporting templates.",
                    ProjectId = projects["PRJ-2026-002"].Id,
                    AssignedToEmployeeId = employees["fady.ibrahim@company.com"].Id,
                    CreatedByUserId = users["yassin.ali@company.com"].Id,
                    Priority = TaskPriority.Low,
                    Status = TaskStatus.Cancelled,
                    DueDate = now.AddDays(20),
                    EstimatedHours = 12,
                    ActualHours = 2,
                    CreatedAt = now.AddDays(-6),
                    UpdatedAt = now.AddDays(-1)
                }
            };

            context.TaskItems.AddRange(tasks);
            await context.SaveChangesAsync();

            var comments = new List<TaskComment>
            {
                new() { TaskId = tasks[0].Id, UserId = users["mona.farouk@company.com"].Id, Content = "Please attach the final API gateway topology before sign-off.", CreatedAt = now.AddDays(-3) },
                new() { TaskId = tasks[0].Id, UserId = users["nada.mostafa@company.com"].Id, Content = "Shared draft v2 and waiting for security review.", CreatedAt = now.AddDays(-2) },
                new() { TaskId = tasks[2].Id, UserId = users["ceo@company.com"].Id, Content = "Escalated blocker to infrastructure procurement.", CreatedAt = now.AddDays(-1) },
                new() { TaskId = tasks[4].Id, UserId = users["yassin.ali@company.com"].Id, Content = "Pilot users approved the first form workflow.", CreatedAt = now.AddDays(-1) }
            };

            context.TaskComments.AddRange(comments);
            await context.SaveChangesAsync();

            logger.LogInformation("Seeded {TaskCount} tasks and {CommentCount} comments.", tasks.Count, comments.Count);

        }

        private static async Task SeedAuditAndResetDataAsync(ApplicationDbContext context, Dictionary<string, ApplicationUser> users, DateTime now, ILogger logger)
        {
            // Check if audit logs already exist
            if (await context.AuditLogs.AnyAsync())
            {
                logger.LogInformation("Audit and reset data already exist. Skipping.");
                return;
            }

            logger.LogInformation("Seeding audit and reset data...");


            context.PasswordResetRequests.AddRange(
                new PasswordResetRequest
                {
                    UserId = users["hany.saad@company.com"].Id,
                    UserEmail = "hany.saad@company.com",
                    TicketNumber = "RST-2026-000101",
                    Status = ResetStatus.Pending,
                    RequestedAt = now.AddMinutes(-40),
                    ExpiresAt = now.AddMinutes(20),
                    IpAddress = "10.10.1.11",
                    UserAgent = "Chrome/126.0"
                },
                new PasswordResetRequest
                {
                    UserId = users["reem.gamal@company.com"].Id,
                    UserEmail = "reem.gamal@company.com",
                    TicketNumber = "RST-2026-000102",
                    Status = ResetStatus.Approved,
                    RequestedAt = now.AddHours(-4),
                    ExpiresAt = now.AddHours(-3),
                    ResolvedAt = now.AddHours(-3).AddMinutes(10),
                    ResolvedBy = "it.admin@company.com",
                    IpAddress = "10.10.1.12",
                    UserAgent = "Edge/125.0"
                });

            context.AuditLogs.AddRange(
                new AuditLog
                {
                    UserId = users["ceo@company.com"].Id,
                    UserEmail = "ceo@company.com",
                    Action = "SEED_DATABASE",
                    ResourceType = "System",
                    ResourceId = null,
                    Details = "Initial QA baseline data seeded",
                    Succeeded = true,
                    Timestamp = now,
                    IpAddress = "127.0.0.1",
                    UserAgent = "Seeder"
                },
                new AuditLog
                {
                    UserId = users["mona.farouk@company.com"].Id,
                    UserEmail = "mona.farouk@company.com",
                    Action = "CREATE_TASK",
                    ResourceType = "TaskItem",
                    ResourceId = null,
                    Details = "Created seeded task for ERP modernization stream",
                    Succeeded = true,
                    Timestamp = now.AddMinutes(-10),
                    IpAddress = "10.10.2.15",
                    UserAgent = "Seeder"
                });

            await context.SaveChangesAsync();

            logger.LogInformation("Audit and reset data seeded.");

        }
        private static async Task EnsureUserInRolesAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, params string[] roles)
        {
            foreach (var role in roles)
            {
                if (!await userManager.IsInRoleAsync(user, role))
                {
                    var roleResult = await userManager.AddToRoleAsync(user, role);
                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException($"Failed assigning role {role} to {user.Email}: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                    }
                }
            }
        }

        private static async Task EnsureDatabaseReadyAsync(ApplicationDbContext context, ILogger logger)
        {
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
                return;
            }

            logger.LogInformation("Non-relational provider '{ProviderName}' detected. Using EnsureCreatedAsync instead of migrations.", context.Database.ProviderName);
            await context.Database.EnsureCreatedAsync();
        }
    }
}
