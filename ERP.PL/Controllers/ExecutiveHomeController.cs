using ERP.BLL.DTOs;
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
    [Authorize(Roles = "CEO")]
    public class ExecutiveHomeController : Controller
    {
        private readonly ILogger<ExecutiveHomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDashboardIntelligenceService _intelligenceService;
        private readonly ITeamHealthService _teamHealthService;
        private readonly IAuditAnomalyService _auditAnomalyService;
        private readonly ITaskRiskService _taskRiskService;

        public ExecutiveHomeController(
            ILogger<ExecutiveHomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IDashboardIntelligenceService intelligenceService,
            ITeamHealthService teamHealthService,
            IAuditAnomalyService auditAnomalyService,
            ITaskRiskService taskRiskService)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _intelligenceService = intelligenceService;
            _teamHealthService = teamHealthService;
            _auditAnomalyService = auditAnomalyService;
            _taskRiskService = taskRiskService;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(currentUser);
            var primaryRole = roles.Contains("CEO") ? "CEO" : "ITAdmin";

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var oneWeekAgo = now.AddDays(-7);
            var twoWeeksAgo = now.AddDays(-14);

            var viewModel = new ExecutiveHomeDashboardViewModel
            {
                UserDisplayName = currentUser.UserName ?? "Executive",
                UserRole = primaryRole
            };

            // ── KPI Overview ──
            viewModel.ActiveDepartments = await _context.Departments.CountAsync(d => !d.IsDeleted);
            viewModel.TotalEmployees = await _context.Employees.CountAsync(e => !e.IsDeleted && e.IsActive);
            viewModel.ActiveProjects = await _context.Projects
                .CountAsync(p => !p.IsDeleted && p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled);

            // System health
            var totalUserAccounts = await _userManager.Users.CountAsync();
            var activeHealthyAccounts = await _userManager.Users
                .CountAsync(u => u.IsActive && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow));
            viewModel.SystemHealthPercentage = totalUserAccounts == 0
                ? 100
                : Math.Clamp((int)Math.Round((activeHealthyAccounts / (double)totalUserAccounts) * 100), 0, 100);

            // ── Task KPIs ──
            var allActiveTasks = await _context.TaskItems
                .Where(t => t.Status != ERP.DAL.Models.TaskStatus.Completed
                         && t.Status != ERP.DAL.Models.TaskStatus.Cancelled)
                .ToListAsync();

            viewModel.TotalActiveTasks = allActiveTasks.Count;
            viewModel.OverdueTaskCount = allActiveTasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < now);
            viewModel.CompletedTasksThisMonth = await _context.TaskItems
                .CountAsync(t => t.Status == ERP.DAL.Models.TaskStatus.Completed
                              && t.CompletedAt.HasValue && t.CompletedAt.Value >= monthStart);

            // ── Intelligence Data ──
            try
            {
                viewModel.Intelligence = await _intelligenceService.GetIntelligenceAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load intelligence data for executive dashboard");
            }

            // ── Team Health ──
            try
            {
                viewModel.TeamHealth = await _teamHealthService.CalculateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute team health for executive dashboard");
            }

            // ── Anomaly Alerts ──
            try
            {
                var anomalies = await _auditAnomalyService.DetectAnomaliesAsync(lookbackHours: 48);
                viewModel.AnomalyAlerts = anomalies
                    .Take(10)
                    .Select(a => new AnomalyAlertItem
                    {
                        Type = a.AnomalyType,
                        Description = a.Description,
                        Severity = a.Severity,
                        SeverityBadgeClass = a.Severity switch
                        {
                            "Critical" => "bg-danger",
                            "Warning" => "bg-warning text-dark",
                            _ => "bg-info"
                        },
                        DetectedAt = a.DetectedAt
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load anomaly alerts for executive dashboard");
            }

            // ── High Risk Tasks ──
            try
            {
                var tasksWithRisk = await _context.TaskItems
                    .Include(t => t.AssignedToEmployee)
                    .Include(t => t.Project)
                    .Where(t => t.Status != ERP.DAL.Models.TaskStatus.Completed
                             && t.Status != ERP.DAL.Models.TaskStatus.Cancelled)
                    .ToListAsync();

                viewModel.HighRiskTasks = tasksWithRisk
                    .Select(t =>
                    {
                        var risk = _taskRiskService.CalculateRisk(t);
                        return new { Task = t, Risk = risk };
                    })
                    .Where(x => x.Risk.Score >= 60)
                    .OrderByDescending(x => x.Risk.Score)
                    .Take(10)
                    .Select(x => new ExecutiveTaskItem
                    {
                        Id = x.Task.Id,
                        Title = x.Task.Title,
                        AssigneeName = x.Task.AssignedToEmployee != null
                            ? $"{x.Task.AssignedToEmployee.FirstName} {x.Task.AssignedToEmployee.LastName}"
                            : "Unassigned",
                        ProjectName = x.Task.Project?.ProjectName ?? "N/A",
                        RiskScore = x.Risk.Score,
                        RiskLevel = x.Risk.Level,
                        DueDate = x.Task.DueDate
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate high risk tasks for executive dashboard");
            }

            // ── Overdue Trend ──
            viewModel.OverdueThisWeek = allActiveTasks
                .Count(t => t.DueDate.HasValue && t.DueDate.Value < now && t.DueDate.Value >= oneWeekAgo);
            viewModel.OverdueLastWeek = await _context.TaskItems
                .CountAsync(t => t.Status != ERP.DAL.Models.TaskStatus.Completed
                              && t.Status != ERP.DAL.Models.TaskStatus.Cancelled
                              && t.DueDate != null && t.DueDate < oneWeekAgo && t.DueDate >= twoWeeksAgo);
            viewModel.OverdueTasksTrend = viewModel.OverdueThisWeek - viewModel.OverdueLastWeek;

            // ── Department Performance ──
            viewModel.DepartmentPerformance = await _context.Departments
                .Where(d => !d.IsDeleted)
                .Select(d => new DepartmentKpi
                {
                    Id = d.Id,
                    DepartmentName = d.DepartmentName,
                    EmployeeCount = d.Employees.Count(e => !e.IsDeleted && e.IsActive),
                    ActiveTasks = _context.TaskItems.Count(t =>
                        t.AssignedToEmployee != null && t.AssignedToEmployee.DepartmentId == d.Id
                        && t.Status != ERP.DAL.Models.TaskStatus.Completed
                        && t.Status != ERP.DAL.Models.TaskStatus.Cancelled),
                    CompletedTasks = _context.TaskItems.Count(t =>
                        t.AssignedToEmployee != null && t.AssignedToEmployee.DepartmentId == d.Id
                        && t.Status == ERP.DAL.Models.TaskStatus.Completed),
                    OverdueTasks = _context.TaskItems.Count(t =>
                        t.AssignedToEmployee != null && t.AssignedToEmployee.DepartmentId == d.Id
                        && t.DueDate != null && t.DueDate < now
                        && t.Status != ERP.DAL.Models.TaskStatus.Completed
                        && t.Status != ERP.DAL.Models.TaskStatus.Cancelled)
                })
                .ToListAsync();

            foreach (var dept in viewModel.DepartmentPerformance)
            {
                var total = dept.ActiveTasks + dept.CompletedTasks;
                dept.CompletionRate = total == 0 ? 0 : Math.Round(100.0 * dept.CompletedTasks / total, 1);
            }

            // ── Digest Insights ──
            viewModel.DigestInsights = BuildDigestInsights(viewModel);

            return View(viewModel);
        }

        private static List<DigestInsight> BuildDigestInsights(ExecutiveHomeDashboardViewModel vm)
        {
            var insights = new List<DigestInsight>();

            if (vm.OverdueTaskCount > 0)
            {
                insights.Add(new DigestInsight
                {
                    Icon = "fas fa-exclamation-circle",
                    Title = $"{vm.OverdueTaskCount} Overdue Tasks",
                    Description = vm.OverdueTasksTrend > 0
                        ? $"Overdue tasks increased by {vm.OverdueTasksTrend} compared to last week. Immediate attention required."
                        : vm.OverdueTasksTrend < 0
                            ? $"Overdue tasks decreased by {Math.Abs(vm.OverdueTasksTrend)} from last week. Positive trend."
                            : "Overdue task count is stable from last week.",
                    Category = "Attention"
                });
            }

            if (vm.Intelligence?.OverloadedEmployeeCount > 0)
            {
                insights.Add(new DigestInsight
                {
                    Icon = "fas fa-user-clock",
                    Title = $"{vm.Intelligence.OverloadedEmployeeCount} Overloaded Staff",
                    Description = "Some team members have excessive workloads. Consider redistributing tasks to prevent burnout.",
                    Category = "Workforce"
                });
            }

            if (vm.Intelligence?.BehindScheduleProjectCount > 0)
            {
                insights.Add(new DigestInsight
                {
                    Icon = "fas fa-calendar-times",
                    Title = $"{vm.Intelligence.BehindScheduleProjectCount} Behind Schedule",
                    Description = "Projects falling behind their timelines. Review resource allocation and deadlines.",
                    Category = "Projects"
                });
            }

            if (vm.TeamHealth != null && vm.TeamHealth.Score < 70)
            {
                insights.Add(new DigestInsight
                {
                    Icon = "fas fa-heart-broken",
                    Title = $"Team Health: {vm.TeamHealth.Score}%",
                    Description = string.Join(". ", vm.TeamHealth.RiskFactors.Take(2)),
                    Category = "Team"
                });
            }

            if (vm.CompletedTasksThisMonth > 0)
            {
                insights.Add(new DigestInsight
                {
                    Icon = "fas fa-check-circle",
                    Title = $"{vm.CompletedTasksThisMonth} Tasks Completed",
                    Description = "Tasks completed this month across all departments.",
                    Category = "Progress"
                });
            }

            if (vm.AnomalyAlerts.Count > 0)
            {
                insights.Add(new DigestInsight
                {
                    Icon = "fas fa-shield-alt",
                    Title = $"{vm.AnomalyAlerts.Count} Anomalies Detected",
                    Description = "Security or operational anomalies detected in the last 48 hours. Review the alerts section.",
                    Category = "Security"
                });
            }

            return insights;
        }
    }
}
