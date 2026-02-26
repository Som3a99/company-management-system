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
using ERP.PL.Middleware;
using ERP.PL.Security;
using ERP.PL.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.IO.Compression;
using System.Threading.RateLimiting;
namespace ERP.PL
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            #region Configure Services
            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddHealthChecks();

            // Database Context - Scoped lifetime
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });

                // Enable sensitive data logging in development only
                if (builder.Environment.IsDevelopment())
                {
                    options.EnableDetailedErrors();
                }
            });

            // Repository Pattern -Scoped lifetime(one per request)
            var keysPath = builder.Configuration["DataProtection:KeysPath"];
            var dataProtectionPath = !string.IsNullOrWhiteSpace(keysPath)
                ? keysPath
                : Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");

            builder.Services.AddDataProtection()
                    .SetApplicationName("ERP-CompanyManagement")
                    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));

            builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<ITaskRepository, TaskRepository>();
            builder.Services.AddScoped<ITaskService, TaskService>();
            builder.Services.AddSingleton<ITaskRiskService, TaskRiskService>();
            builder.Services.AddScoped<IWorkloadService, WorkloadService>();
            builder.Services.AddScoped<IAiNarrativeService, AiNarrativeService>();
            builder.Services.AddScoped<ITaskDescriptionService, TaskDescriptionService>();
            builder.Services.AddScoped<IProjectForecastService, ProjectForecastService>();
            builder.Services.AddHttpClient("AiService", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            builder.Services.AddScoped<IReportingService, ReportingService>();
            builder.Services.AddScoped<IReportJobService>(sp =>
            {
                var dbContext = sp.GetRequiredService<ApplicationDbContext>();
                var reportingService = sp.GetRequiredService<IReportingService>();
                var logger = sp.GetRequiredService<ILogger<ReportJobService>>();
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
                return new ReportJobService(dbContext, reportingService, logger, webRoot);
            });
            builder.Services.AddHostedService<ReportJobWorkerService>();
            builder.Services.AddHostedService<CacheTelemetryHostedService>();
            builder.Services.AddHostedService<CacheWarmupHostedService>();

            // Phase 3 — Proactive Intelligence
            builder.Services.AddScoped<ITaskAssignmentSuggestionService, TaskAssignmentSuggestionService>();
            builder.Services.AddScoped<IAuditAnomalyService, AuditAnomalyService>();
            builder.Services.AddScoped<ITeamHealthService, TeamHealthService>();
            builder.Services.AddScoped<IDashboardIntelligenceService, DashboardIntelligenceService>();
            builder.Services.AddScoped<IExecutiveDigestService, ExecutiveDigestService>();

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
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

            //  Add response compression
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json" });
            });
            builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            builder.Services.AddMemoryCache(options =>
            {
                options.SizeLimit = 1024;
                options.CompactionPercentage = 0.25;
                options.TrackStatistics = true;
            });
            builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
            builder.Services.AddResponseCaching();

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("ReportingHeavy", context =>
                    RateLimitPartition.GetTokenBucketLimiter(
                        partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = 30,
                            QueueLimit = 0,
                            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                            TokensPerPeriod = 30,
                            AutoReplenishment = true
                        }));
            });


            // LOGGING: Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddEventSourceLogger();

            if (builder.Environment.IsDevelopment())
                builder.Logging.AddDebug();

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
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // Production: catch unhandled exceptions and return safe error responses
                app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
                app.UseHsts();
            }

            app.UseForwardedHeaders();
            if (!app.Environment.IsEnvironment("Testing"))
            {
                app.UseHttpsRedirection();
            }

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
                    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
                }
                await next();
            });

            // PERFORMANCE: Enable response compression
            app.UseResponseCompression();
            app.UseResponseCaching();

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context =>
                {
                    var fileName = context.File.Name;
                    var responseHeaders = context.Context.Response.Headers;

                    if (fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    {
                        responseHeaders.Append("Cache-Control", "public,max-age=31536000,immutable");
                        return;
                    }

                    if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    {
                        responseHeaders.Append("Cache-Control", "public,max-age=2592000");
                    }
                }
            });

            // Serve generated report files from wwwroot/reports
            var reportJobsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "reports", "jobs");
            Directory.CreateDirectory(reportJobsPath);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(reportJobsPath),
                RequestPath = "/reports/jobs",
                ServeUnknownFileTypes = false,
                ContentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider(
                    new Dictionary<string, string>
                    {
                        { ".pdf", "application/pdf" },
                        { ".csv", "text/csv" },
                        { ".xls", "application/vnd.ms-excel" },
                        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                        { ".html", "text/html" }
                    })
            });

            app.UseRouting();
            app.UseRateLimiter();
            app.UseStatusCodePages(async context =>
            {
                if (context.HttpContext.Response.StatusCode == StatusCodes.Status413PayloadTooLarge)
                {
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync("{\"error\":\"File size exceeds the 10MB limit.\"}");
                }
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapHealthChecks("/health");

            app.MapGet("/health/cache", (IMemoryCache memoryCache) =>
            {
                var stats = (memoryCache as MemoryCache)?.GetCurrentStatistics();
                return Results.Ok(new
                {
                    Status = "Healthy",
                    Cache = new
                    {
                        stats?.CurrentEntryCount,
                        stats?.CurrentEstimatedSize,
                        stats?.TotalHits,
                        stats?.TotalMisses
                    }
                });
            });

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            #endregion

            if (!app.Environment.IsEnvironment("Testing"))
            {
                await ApplyDatabaseMigrationsAndSeedAsync(app);
            }

            app.Run();
        }

        #region Helper Method
        private static async Task ApplyDatabaseMigrationsAndSeedAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                var shouldApplyMigrations = app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
                if (shouldApplyMigrations)
                {
                    if (dbContext.Database.IsRelational())
                    {
                        await dbContext.Database.MigrateAsync();
                        logger.LogInformation("Database migrations applied successfully during startup.");
                    }
                    else
                    {
                        // Non-relational providers (InMemory, SQLite in-memory) don't support migrations
                        await dbContext.Database.EnsureCreatedAsync();
                        logger.LogInformation("Database schema created using EnsureCreatedAsync (non-relational provider detected).");
                    }
                }

                var seedMode = app.Configuration["Seed:Mode"]?.Trim();
                if (string.IsNullOrWhiteSpace(seedMode) || seedMode.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Seed:Mode is None. Skipping data seeding.");
                    return;
                }

                if (seedMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
                {
                    var resetDatabase = app.Configuration.GetValue<bool>("Seed:ResetDatabase");
                    await SystemDataSeeder.SeedAsync(dbContext, userManager, roleManager, logger, "Development", resetDatabase);
                    return;
                }

                if (seedMode.Equals("Production", StringComparison.OrdinalIgnoreCase))
                {
                    await SystemDataSeeder.SeedProductionAsync(dbContext, userManager, roleManager, app.Configuration, logger);
                    return;
                }

                logger.LogWarning("Unsupported Seed:Mode value '{SeedMode}'. Supported values: None, Development, Production.", seedMode);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Database migration/seed failed during startup.");
                throw;
            }
        }
        #endregion
    }
}
