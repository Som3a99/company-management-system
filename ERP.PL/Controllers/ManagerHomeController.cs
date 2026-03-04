using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Controllers
{
    [Authorize(Roles = "CEO,DepartmentManager,ProjectManager")]
    public class ManagerHomeController : Controller
    {
        private readonly ILogger<ManagerHomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITaskRiskService _taskRiskService;

        public ManagerHomeController(
            ILogger<ManagerHomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITaskRiskService taskRiskService)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _taskRiskService = taskRiskService;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(currentUser);
            var primaryRole = roles.Contains("DepartmentManager") ? "DepartmentManager" : "ProjectManager";

            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.ManagedDepartment)
                .Include(e => e.ManagedProject)
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUser.Id && !e.IsDeleted);

            var viewModel = new ManagerHomeDashboardViewModel
            {
                UserDisplayName = currentUser.UserName ?? "Manager",
                UserRole = primaryRole
            };

            if (employee == null)
                return View(viewModel);

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // Determine team scope based on role
            IQueryable<TaskItem> teamTasksQuery;
            IQueryable<Employee> teamMembersQuery;
            IQueryable<Project> projectsQuery;

            if (primaryRole == "DepartmentManager" && employee.ManagedDepartment != null)
            {
                viewModel.DepartmentName = employee.ManagedDepartment.DepartmentName;

                // All employees in the department
                teamMembersQuery = _context.Employees
                    .Where(e => e.DepartmentId == employee.ManagedDepartment.Id && !e.IsDeleted);

                // All tasks assigned to department employees
                teamTasksQuery = _context.TaskItems
                    .Include(t => t.AssignedToEmployee)
                    .Include(t => t.Project)
                    .Where(t => t.AssignedToEmployee != null && t.AssignedToEmployee.DepartmentId == employee.ManagedDepartment.Id);

                // Department projects
                projectsQuery = _context.Projects
                    .Where(p => p.DepartmentId == employee.ManagedDepartment.Id && !p.IsDeleted);
            }
            else if (employee.ManagedProject != null)
            {
                viewModel.DepartmentName = employee.Department?.DepartmentName ?? "N/A";

                // Project team members
                var projectEmployeeIds = await _context.ProjectEmployees
                    .Where(pe => pe.ProjectId == employee.ManagedProject.Id)
                    .Select(pe => pe.EmployeeId)
                    .ToListAsync();

                teamMembersQuery = _context.Employees
                    .Where(e => projectEmployeeIds.Contains(e.Id) && !e.IsDeleted);

                // Tasks in this project
                teamTasksQuery = _context.TaskItems
                    .Include(t => t.AssignedToEmployee)
                    .Include(t => t.Project)
                    .Where(t => t.ProjectId == employee.ManagedProject.Id);

                // Only managed project
                projectsQuery = _context.Projects
                    .Where(p => p.Id == employee.ManagedProject.Id && !p.IsDeleted);
            }
            else
            {
                return View(viewModel);
            }

            // ── Team Members ──
            viewModel.TeamMembers = await teamMembersQuery
                .Select(e => new TeamMemberSummary
                {
                    EmployeeId = e.Id,
                    Name = e.FirstName + " " + e.LastName,
                    Position = e.Position,
                    ActiveTasks = e.AssignedTasks.Count(t => t.Status != ERP.DAL.Models.TaskStatus.Completed
                                                         && t.Status != ERP.DAL.Models.TaskStatus.Cancelled),
                    OverdueTasks = e.AssignedTasks.Count(t => t.DueDate != null && t.DueDate < now
                                                          && t.Status != ERP.DAL.Models.TaskStatus.Completed
                                                          && t.Status != ERP.DAL.Models.TaskStatus.Cancelled)
                })
                .Take(20)
                .ToListAsync();

            // Calculate workload labels
            foreach (var member in viewModel.TeamMembers)
            {
                if (member.ActiveTasks >= 8)
                {
                    member.WorkloadLabel = "Overloaded";
                    member.WorkloadBadgeClass = "bg-danger";
                }
                else if (member.ActiveTasks >= 5)
                {
                    member.WorkloadLabel = "Heavy";
                    member.WorkloadBadgeClass = "bg-warning text-dark";
                }
                else if (member.ActiveTasks >= 1)
                {
                    member.WorkloadLabel = "Normal";
                    member.WorkloadBadgeClass = "bg-success";
                }
                else
                {
                    member.WorkloadLabel = "Available";
                    member.WorkloadBadgeClass = "bg-info";
                }
            }

            viewModel.TeamMemberCount = viewModel.TeamMembers.Count;

            // ── Task Metrics ──
            var allTeamTasks = await teamTasksQuery
                .Where(t => t.Status != ERP.DAL.Models.TaskStatus.Cancelled)
                .ToListAsync();

            var activeTasks = allTeamTasks
                .Where(t => t.Status != ERP.DAL.Models.TaskStatus.Completed)
                .ToList();

            viewModel.TotalActiveTasks = activeTasks.Count;
            viewModel.OverdueTaskCount = activeTasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < now);
            viewModel.HighPriorityTaskCount = activeTasks.Count(t => t.Priority >= TaskPriority.High);
            viewModel.BlockedTaskCount = activeTasks.Count(t => t.Status == ERP.DAL.Models.TaskStatus.Blocked);

            // ── Overdue Tasks ──
            viewModel.OverdueTasks = activeTasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value < now)
                .OrderBy(t => t.DueDate)
                .Take(10)
                .Select(t => MapToManagerTaskItem(t, null))
                .ToList();

            // ── High Risk Tasks ──
            try
            {
                var riskyTasks = activeTasks
                    .Select(t =>
                    {
                        var risk = _taskRiskService.CalculateRisk(t);
                        return (Task: t, Risk: risk);
                    })
                    .Where(x => x.Risk.Score >= 50)
                    .OrderByDescending(x => x.Risk.Score)
                    .Take(10)
                    .Select(x => MapToManagerTaskItem(x.Task, x.Risk))
                    .ToList();

                viewModel.HighRiskTasks = riskyTasks;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate task risks for manager dashboard");
            }

            // ── Project Progress ──
            viewModel.Projects = await projectsQuery
                .Select(p => new ManagerProjectProgress
                {
                    Id = p.Id,
                    ProjectName = p.ProjectName,
                    Status = p.Status.ToString(),
                    StatusBadgeClass = GetProjectStatusBadge(p.Status),
                    TotalTasks = p.Tasks.Count(t => t.Status != ERP.DAL.Models.TaskStatus.Cancelled),
                    CompletedTasks = p.Tasks.Count(t => t.Status == ERP.DAL.Models.TaskStatus.Completed),
                    OverdueTasks = p.Tasks.Count(t => t.DueDate != null && t.DueDate < now
                                                   && t.Status != ERP.DAL.Models.TaskStatus.Completed
                                                   && t.Status != ERP.DAL.Models.TaskStatus.Cancelled),
                    ProgressPercent = p.Tasks.Count(t => t.Status != ERP.DAL.Models.TaskStatus.Cancelled) == 0
                        ? 0
                        : (int)Math.Round(100.0 * p.Tasks.Count(t => t.Status == ERP.DAL.Models.TaskStatus.Completed)
                            / p.Tasks.Count(t => t.Status != ERP.DAL.Models.TaskStatus.Cancelled))
                })
                .ToListAsync();

            // ── Department Performance ──
            viewModel.TasksCompletedThisMonth = allTeamTasks
                .Count(t => t.Status == ERP.DAL.Models.TaskStatus.Completed && t.CompletedAt.HasValue && t.CompletedAt.Value >= monthStart);
            viewModel.TasksCreatedThisMonth = allTeamTasks
                .Count(t => t.CreatedAt >= monthStart);
            viewModel.CompletionRate = allTeamTasks.Count == 0 ? 0
                : Math.Round(100.0 * allTeamTasks.Count(t => t.Status == ERP.DAL.Models.TaskStatus.Completed) / allTeamTasks.Count, 1);

            return View(viewModel);
        }

        private static ManagerTaskItem MapToManagerTaskItem(TaskItem task, BLL.DTOs.TaskRiskResult? risk)
        {
            return new ManagerTaskItem
            {
                Id = task.Id,
                Title = task.Title,
                AssigneeName = task.AssignedToEmployee != null
                    ? $"{task.AssignedToEmployee.FirstName} {task.AssignedToEmployee.LastName}"
                    : "Unassigned",
                ProjectName = task.Project?.ProjectName ?? "N/A",
                Priority = task.Priority.ToString(),
                PriorityBadgeClass = GetPriorityBadge(task.Priority),
                Status = task.Status.ToString(),
                DueDate = task.DueDate,
                RiskScore = risk?.Score,
                RiskLevel = risk?.Level ?? "Low"
            };
        }

        private static string GetPriorityBadge(TaskPriority priority) => priority switch
        {
            TaskPriority.Critical => "bg-danger",
            TaskPriority.High => "bg-warning text-dark",
            TaskPriority.Medium => "bg-info",
            TaskPriority.Low => "bg-secondary",
            _ => "bg-light text-dark"
        };

        private static string GetProjectStatusBadge(ProjectStatus status) => status switch
        {
            ProjectStatus.InProgress => "bg-primary",
            ProjectStatus.Completed => "bg-success",
            ProjectStatus.OnHold => "bg-warning text-dark",
            _ => "bg-secondary"
        };
    }
}
