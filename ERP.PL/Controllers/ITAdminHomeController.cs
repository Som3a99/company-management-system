using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.PL.Controllers
{
    [Authorize(Roles = "ITAdmin")]
    public class ITAdminHomeController : Controller
    {
        private readonly ILogger<ITAdminHomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditAnomalyService _anomalyService;
        private readonly IMemoryCache _memoryCache;

        public ITAdminHomeController(
            ILogger<ITAdminHomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditAnomalyService anomalyService,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _anomalyService = anomalyService;
            _memoryCache = memoryCache;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var now = DateTime.UtcNow;
            var last24h = now.AddHours(-24);
            var last48h = now.AddHours(-48);
            var last7d = now.AddDays(-7);

            var vm = new ITAdminHomeDashboardViewModel
            {
                UserDisplayName = currentUser.UserName ?? "IT Admin",
                ServerTimeUtc = now
            };

            // ── System Health ──
            try
            {
                // Database connectivity — if we got this far, DB is connected
                vm.DatabaseStatus = "Connected";
                vm.DatabaseStatusClass = "status-green";
                vm.ApplicationStatus = "Operational";
                vm.ApplicationStatusClass = "status-green";

                // Active sessions (users who logged in within last 30 min based on lockout activity or recent audit)
                vm.ActiveSessionCount = await _context.AuditLogs
                    .Where(a => a.Timestamp >= now.AddMinutes(-30) && a.Succeeded)
                    .Select(a => a.UserId)
                    .Distinct()
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute system health metrics");
                vm.DatabaseStatus = "Error";
                vm.DatabaseStatusClass = "status-red";
                vm.ApplicationStatus = "Degraded";
                vm.ApplicationStatusClass = "status-amber";
            }

            // ── Security Monitoring ──
            try
            {
                vm.FailedLoginAttempts24h = await _context.AuditLogs
                    .CountAsync(a => a.Timestamp >= last24h && !a.Succeeded
                        && (a.Action == "LOGIN" || a.Action == "LOGIN_FAILED"));

                vm.LockedAccountCount = await _userManager.Users
                    .CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);

                vm.RoleChanges7d = await _context.AuditLogs
                    .CountAsync(a => a.Timestamp >= last7d
                        && (a.Action == "ASSIGN_ROLE" || a.Action == "REMOVE_ROLE" || a.Action == "ROLE_CHANGE"));

                vm.PasswordResets7d = await _context.PasswordResetRequests
                    .CountAsync(r => r.RequestedAt >= last7d);

                // Anomaly detection
                try
                {
                    var anomalies = await _anomalyService.DetectAnomaliesAsync(lookbackHours: 48);
                    vm.SuspiciousActivities48h = anomalies.Count;
                    vm.WarningAnomalies = anomalies.Count(a => a.Severity == "Warning");
                    vm.CriticalAnomalies = anomalies.Count(a => a.Severity == "Critical");

                    vm.RecentSecurityEvents = anomalies
                        .OrderByDescending(a => a.DetectedAt)
                        .Take(8)
                        .Select(a => new SecurityEventItem
                        {
                            Icon = a.Severity == "Critical" ? "fas fa-exclamation-circle"
                                 : a.Severity == "Warning" ? "fas fa-exclamation-triangle"
                                 : "fas fa-info-circle",
                            Title = a.AnomalyType,
                            Description = a.Description,
                            Severity = a.Severity,
                            SeverityClass = a.Severity switch
                            {
                                "Critical" => "status-red",
                                "Warning" => "status-amber",
                                _ => "status-blue"
                            },
                            Timestamp = a.DetectedAt
                        })
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load anomaly data for IT admin dashboard");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute security monitoring metrics");
            }

            // ── Infrastructure Metrics ──
            try
            {
                vm.PendingReportJobs = await _context.ReportJobs
                    .CountAsync(r => r.Status == ReportJobStatus.Pending);
                vm.ProcessingReportJobs = await _context.ReportJobs
                    .CountAsync(r => r.Status == ReportJobStatus.Processing);
                vm.FailedReportJobs = await _context.ReportJobs
                    .CountAsync(r => r.Status == ReportJobStatus.Failed);
                vm.CompletedReportJobs24h = await _context.ReportJobs
                    .CountAsync(r => r.Status == ReportJobStatus.Completed
                        && r.CompletedAtUtc.HasValue && r.CompletedAtUtc.Value >= last24h);

                // Cache health
                var memoryStats = (_memoryCache as MemoryCache)?.GetCurrentStatistics();
                var totalLookups = (memoryStats?.TotalHits ?? 0) + (memoryStats?.TotalMisses ?? 0);
                vm.CacheHitRatio = totalLookups == 0 ? 0 : Math.Round((memoryStats?.TotalHits ?? 0) * 100.0 / totalLookups, 1);
                vm.CacheEntryCount = memoryStats?.CurrentEntryCount ?? 0;
                vm.CacheEstimatedSize = memoryStats?.CurrentEstimatedSize ?? 0;
                if (vm.CacheEntryCount > 900)
                    vm.CachePressureWarning = "Cache entry count is near configured limit. Review eviction and TTLs.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute infrastructure metrics");
            }

            // ── Identity & Access Overview ──
            try
            {
                vm.TotalUserAccounts = await _userManager.Users.CountAsync();
                vm.ActiveUserAccounts = await _userManager.Users.CountAsync(u => u.IsActive);
                vm.InactiveUserAccounts = vm.TotalUserAccounts - vm.ActiveUserAccounts;
                vm.UsersRequiringPasswordChange = await _userManager.Users
                    .CountAsync(u => u.RequirePasswordChange);
                vm.AccountsCreatedThisWeek = await _userManager.Users
                    .CountAsync(u => u.CreatedAt >= last7d);

                // Role distribution
                var roleNames = new[] { "CEO", "ITAdmin", "DepartmentManager", "ProjectManager", "Employee" };
                var roleDist = new List<RoleDistributionItem>();
                foreach (var role in roleNames)
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                    roleDist.Add(new RoleDistributionItem
                    {
                        RoleName = role,
                        Count = usersInRole.Count,
                        BadgeClass = role switch
                        {
                            "CEO" => "badge-role-ceo",
                            "ITAdmin" => "badge-role-admin",
                            "DepartmentManager" => "badge-role-manager",
                            "ProjectManager" => "badge-role-pm",
                            _ => "badge-role-employee"
                        }
                    });
                }
                vm.RoleDistribution = roleDist;

                // Users without any role
                var allUsers = await _userManager.Users.ToListAsync();
                var usersWithoutRole = 0;
                foreach (var user in allUsers)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    if (!roles.Any()) usersWithoutRole++;
                }
                // Store as InactiveUserAccounts override if there are roleless users
                // (we can show this separately)
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute identity & access metrics");
            }

            // ── Audit & Logs ──
            try
            {
                vm.TotalAuditEvents24h = await _context.AuditLogs
                    .CountAsync(a => a.Timestamp >= last24h);
                vm.ErrorEvents24h = await _context.AuditLogs
                    .CountAsync(a => a.Timestamp >= last24h && !a.Succeeded);

                vm.RecentAuditEvents = await _context.AuditLogs
                    .OrderByDescending(a => a.Timestamp)
                    .Take(12)
                    .Select(a => new AuditEventItem
                    {
                        Action = a.Action,
                        UserEmail = a.UserEmail,
                        ResourceType = a.ResourceType,
                        Succeeded = a.Succeeded,
                        Timestamp = a.Timestamp,
                        IpAddress = a.IpAddress
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load audit log data");
            }

            // ── Password Resets ──
            try
            {
                vm.PendingResetRequests = await _context.PasswordResetRequests
                    .CountAsync(r => r.Status == ResetStatus.Pending);
                vm.ExpiredResetRequests = await _context.PasswordResetRequests
                    .CountAsync(r => r.Status == ResetStatus.Pending && r.ExpiresAt < now);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load password reset data");
            }

            // ── Quick Stats ──
            try
            {
                vm.TotalDepartments = await _context.Departments.CountAsync(d => !d.IsDeleted);
                vm.TotalEmployees = await _context.Employees.CountAsync(e => !e.IsDeleted && e.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load quick stats");
            }

            return View(vm);
        }
    }
}
