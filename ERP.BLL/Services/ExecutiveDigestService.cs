using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.BLL.Services
{
    public class ExecutiveDigestService : IExecutiveDigestService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITaskRiskService _taskRiskService;
        private readonly ITeamHealthService _teamHealthService;
        private readonly IAuditAnomalyService _anomalyService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ExecutiveDigestService> _logger;

        public ExecutiveDigestService(
            ApplicationDbContext context,
            ITaskRiskService taskRiskService,
            ITeamHealthService teamHealthService,
            IAuditAnomalyService anomalyService,
            IMemoryCache cache,
            ILogger<ExecutiveDigestService> logger)
        {
            _context = context;
            _taskRiskService = taskRiskService;
            _teamHealthService = teamHealthService;
            _anomalyService = anomalyService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<WeeklyDigestData> PrepareDigestAsync()
        {
            var cacheKey = "erp:digest:weekly";

            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                entry.Size = 1;
                return await PrepareDigestCoreAsync();
            });

            return result ?? new WeeklyDigestData();
        }

        private async Task<WeeklyDigestData> PrepareDigestCoreAsync()
        {
            var now = DateTime.UtcNow;
            var weekStart = now.AddDays(-7);

            var digest = new WeeklyDigestData
            {
                GeneratedAt = now,
                PeriodStart = weekStart,
                PeriodEnd = now
            };

            // ── Task Summary ──
            var allActiveTasks = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled)
                .ToListAsync();

            digest.TotalActiveTasks = allActiveTasks.Count;

            digest.OverdueTaskCount = allActiveTasks
                .Count(t => t.DueDate.HasValue && t.DueDate.Value.Date < now.Date);

            // High risk count
            var highRiskCount = 0;
            foreach (var task in allActiveTasks)
            {
                var risk = _taskRiskService.CalculateRisk(task);
                if (risk.Level == "High")
                    highRiskCount++;
            }
            digest.HighRiskTaskCount = highRiskCount;

            // Tasks created/completed this week
            digest.TasksCreatedThisWeek = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.CreatedAt >= weekStart)
                .CountAsync();

            digest.TasksCompletedThisWeek = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.Status == TaskStatus.Completed
                         && t.CompletedAt.HasValue
                         && t.CompletedAt.Value >= weekStart)
                .CountAsync();

            // ── Workforce ──
            var workloads = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.Status != TaskStatus.Completed
                         && t.Status != TaskStatus.Cancelled
                         && t.AssignedToEmployeeId.HasValue)
                .GroupBy(t => new
                {
                    EmployeeId = t.AssignedToEmployeeId!.Value,
                    EmployeeName = t.AssignedToEmployee.FirstName + " " + t.AssignedToEmployee.LastName
                })
                .Select(g => new
                {
                    g.Key.EmployeeName,
                    ActiveTasks = g.Count(),
                    RemainingHours = (int)g.Sum(x =>
                        (x.EstimatedHours ?? 0m) - x.ActualHours > 0
                            ? (x.EstimatedHours ?? 0m) - x.ActualHours
                            : 0m)
                })
                .ToListAsync();

            var overloaded = workloads
                .Where(w => Math.Clamp((w.ActiveTasks * 5) + (w.RemainingHours / 2), 0, 100) > 70)
                .ToList();

            digest.OverloadedEmployeeCount = overloaded.Count;
            digest.OverloadedEmployeeNames = overloaded
                .Select(w => w.EmployeeName)
                .ToList();

            // ── Projects ──
            var behindProjects = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Status != ProjectStatus.Completed
                         && p.Status != ProjectStatus.Cancelled
                         && !p.IsDeleted
                         && p.EndDate.HasValue
                         && p.EndDate.Value.Date < now.Date)
                .Select(p => p.ProjectName)
                .ToListAsync();

            digest.BehindScheduleProjectCount = behindProjects.Count;
            digest.BehindScheduleProjectNames = behindProjects;

            // ── Security (non-critical — failures must not crash the digest) ──
            try
            {
                var anomalies = await _anomalyService.DetectAnomaliesAsync(168); // 7 days
                digest.AnomaliesDetected = anomalies.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Anomaly detection failed during digest preparation. Defaulting to 0.");
                digest.AnomaliesDetected = 0;
            }

            // ── Team Health (non-critical — failures must not crash the digest) ──
            try
            {
                digest.TeamHealth = await _teamHealthService.CalculateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Team health calculation failed during digest preparation. Using default.");
                digest.TeamHealth = new TeamHealthScoreResult { Score = 0, Status = "Unavailable" };
            }

            _logger.LogInformation(
                "Weekly digest prepared. Active: {Active}, HighRisk: {HighRisk}, Overdue: {Overdue}, Health: {Health}",
                digest.TotalActiveTasks, digest.HighRiskTaskCount,
                digest.OverdueTaskCount, digest.TeamHealth.Status);

            return digest;
        }
    }
}
