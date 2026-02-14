using ERP.BLL.Interfaces;
using ERP.BLL.Reporting.Interfaces;
using ERP.BLL.Reporting.Services;
using ERP.BLL.Repositories;
using ERP.BLL.Services;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.Data;
using ERP.PL.Helpers;
using ERP.PL.Mapping.Department;
using ERP.PL.Mapping.Employee;
using ERP.PL.Mapping.Project;
using ERP.PL.Security;
using ERP.PL.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
namespace ERP.PL
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            #region Configure Services
            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Database Context - Scoped lifetime
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
                // Enable sensitive data logging in development only
                if (builder.Environment.IsDevelopment())
                {
                    options.EnableDetailedErrors();
                }
            });

            // Repository Pattern -Scoped lifetime(one per request)
            builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<ITaskRepository, TaskRepository>();
            builder.Services.AddScoped<ITaskService, TaskService>();
            builder.Services.AddScoped<IReportingService, ReportingService>();
            builder.Services.AddScoped<ITaskService, TaskService>();
            builder.Services.AddScoped<IReportingService, ReportingService>();
            builder.Services.AddScoped<IReportJobService, ReportJobService>();
            builder.Services.AddHostedService<ReportJobWorkerService>();

            // Custom Claims Principal Factory to add extra claims for authorization
            builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>,ApplicationUserClaimsPrincipalFactory>();

            // Role Management Service - Scoped lifetime (depends on DbContext)
            builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();

            // Audit Service - Scoped lifetime (depends on DbContext)
            builder.Services.AddScoped<IAuditService, AuditService>();

            // Department Manager HR Service - Scoped lifetime (depends on DbContext and Identity)
            builder.Services.AddScoped<IDepartmentManagerHrService, DepartmentManagerHrService>();

            // Project Team Service - Scoped lifetime (depends on DbContext and Identity)
            builder.Services.AddScoped<IProjectTeamService, ProjectTeamService>();

            // Auto Mapper Configurations
            builder.Services.AddAutoMapper(cfg => { }, typeof(EmployeeProfile).Assembly);
            builder.Services.AddAutoMapper(cfg => { }, typeof(DepartmentProfile).Assembly);
            builder.Services.AddAutoMapper(cfg => { }, typeof(ProjectProfile).Assembly);

            // Document Settings
            builder.Services.AddScoped<DocumentSettings>();

            // Input Sanitizer - Singleton (stateless utility)
            builder.Services.AddSingleton<InputSanitizer>();

            //  Add Anti-forgery token validation
            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-TOKEN";
                // Use HTTPS only in production; allow HTTP in development
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

            //  Add response compression
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            // LOGGING: Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();

            if (builder.Environment.IsDevelopment())
            {
                builder.Logging.SetMinimumLevel(LogLevel.Information);
            }
            else
            {
                builder.Logging.SetMinimumLevel(LogLevel.Warning);
            }

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
                options.ValueLengthLimit = 10 * 1024 * 1024;
                options.MultipartHeadersLengthLimit = 10 * 1024 * 1024;
            });

            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
            });

            builder.Services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
            });

            // Configure authorization policies
            builder.Services.AddAuthorizationBuilder()
                            // ROLE-BASED POLICIES (Global)
                            // CEO has full system access
                            .AddPolicy("RequireCEO", policy => policy.RequireRole("CEO"))
                            // IT Admin can manage users, system settings
                            .AddPolicy("RequireITAdmin", policy => policy.RequireRole("CEO", "ITAdmin"))
                            // Managers (Department or Project)
                            .AddPolicy("RequireManager", policy => policy.RequireRole("CEO", "DepartmentManager", "ProjectManager"))
                            // CLAIM-BASED POLICIES (Context)
                            // User must be linked to an Employee record
                            .AddPolicy("RequireEmployee", policy => policy.RequireClaim("EmployeeId"))
                            // User must manage a department
                            .AddPolicy("RequireDepartmentManager", policy => policy.RequireClaim("ManagedDepartmentId"))
                            // User must manage a project
                            .AddPolicy("RequireProjectManager", policy => policy.RequireClaim("ManagedProjectId"));

            // Configure Identity
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Password settings (CRITICAL: Balance security vs usability)
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 6;

                // Lockout settings (prevent brute force)
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User settings
                options.User.RequireUniqueEmail = true;

                // Sign-in settings
                options.SignIn.RequireConfirmedEmail = false; // MVP: No email confirmation
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders(); // For password reset tokens

            // Configure cookie settings (SECURITY CRITICAL)
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true; // Prevent XSS access to cookie
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
                options.Cookie.SameSite = SameSiteMode.Strict; // Prevent CSRF
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Session timeout
                options.SlidingExpiration = true; // Extend on activity

                // Login/logout paths
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
            });

            builder.Services.AddHttpContextAccessor();

            // Respect reverse proxy headers for scheme and client IP
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });
            #endregion

            var app = builder.Build();

            #region Configure Kestral Middelware
            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
                app.UseStatusCodePagesWithReExecute("/Error/{0}");
            }

            app.UseHttpsRedirection();

            app.Use(async (context, next) =>
            {
                // Content Security Policy - Prevent XSS
                context.Response.Headers.Append("Content-Security-Policy",
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                    "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                    "img-src 'self' data: https:; " +
                    "font-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com;");

                // X-Content-Type-Options - Prevent MIME sniffing
                context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                // X-Frame-Options - Prevent clickjacking
                context.Response.Headers.Append("X-Frame-Options", "DENY");
                // X-XSS-Protection - Enable browser XSS protection
                context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
                // Referrer-Policy - Control referrer information
                context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
                // HTTP Strict Transport Security (HTTPS only)
                if (context.Request.IsHttps)
                {
                    context.Response.Headers.Append("Strict-Transport-Security",
                        "max-age=31536000; includeSubDomains");
                }
                await next();
            });
            // PERFORMANCE: Enable response compression
            app.UseResponseCompression();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseStatusCodePages(async context =>
            {
                if (context.HttpContext.Response.StatusCode == StatusCodes.Status413PayloadTooLarge)
                {
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync(
                        "{\"error\":\"File size exceeds the 10MB limit.\"}"
                    );
                }
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            #endregion

            #region Seeding Data
            //using (var scope = app.Services.CreateScope())
            //{
            //    var services = scope.ServiceProvider;
            //    var logger = services.GetRequiredService<ILogger<Program>>();

            //    try
            //    {
            //        var dbContext = services.GetRequiredService<ApplicationDbContext>();
            //        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            //        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            //        // Full system reset + seeding for end-to-end manual testing
            //        await SystemDataSeeder.SeedAsync(dbContext, userManager, roleManager, logger);
            //    }
            //    catch (Exception ex)
            //    {
            //        logger.LogCritical(ex, "FATAL: System seeding failed. Application cannot start.");
            //        throw; // Fail startup if seeding fails
            //    }
            //}
            #endregion

            app.Run();
        }
    }
}
