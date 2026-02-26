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
    public class TeamHealthService : ITeamHealthService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITaskRiskService _taskRiskService;
        private readonly IProjectForecastService _forecastService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TeamHealthService> _logger;

        // Deduction thresholds
        internal const double OverdueThreshold = 0.10;   // >10% overdue -> -15
        internal const double BlockedThreshold = 0.05;    // >5% blocked  -> -10
        internal const int OverdueDeduction = 15;
        internal const int BlockedDeduction = 10;
        internal const int OverloadedDeduction = 10;
        internal const int BehindScheduleDeduction = 15;
        internal const int OverloadedLoadScoreThreshold = 70;

        public TeamHealthService(
            ApplicationDbContext context,
            ITaskRiskService taskRiskService,
            IProjectForecastService forecastService,
            IMemoryCache cache,
            ILogger<TeamHealthService> logger)
        {
            _context = context;
            _taskRiskService = taskRiskService;
            _forecastService = forecastService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<TeamHealthScoreResult> CalculateAsync()
        {
            var cacheKey = "erp:team-health";

            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await CalculateCoreAsync();
            });

            return result ?? new TeamHealthScoreResult { Score = 100, Status = "Healthy" };
        }

        private async Task<TeamHealthScoreResult> CalculateCoreAsync()
        {
            var score = 100;
            var riskFactors = new List<string>();

            // Get all active tasks
            var activeTasks = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled)
                .ToListAsync();

            var totalActive = activeTasks.Count;

            if (totalActive > 0)
            {
                // 1. Overdue check: >10% overdue -> -15
                var overdueCount = activeTasks.Count(t =>
                    t.DueDate.HasValue && t.DueDate.Value.Date < DateTime.UtcNow.Date);
                var overdueRatio = (double)overdueCount / totalActive;

                if (overdueRatio > OverdueThreshold)
                {
                    score -= OverdueDeduction;
                    riskFactors.Add($"{overdueCount} overdue tasks ({overdueRatio:P0} of active)");
                }

                // 2. Blocked check: >5% blocked -> -10
                var blockedCount = activeTasks.Count(t => t.Status == TaskStatus.Blocked);
                var blockedRatio = (double)blockedCount / totalActive;

                if (blockedRatio > BlockedThreshold)
                {
                    score -= BlockedDeduction;
                    riskFactors.Add($"{blockedCount} blocked tasks ({blockedRatio:P0} of active)");
                }

                // 3. High-risk tasks check
                var highRiskCount = 0;
                foreach (var task in activeTasks)
                {
                    var risk = _taskRiskService.CalculateRisk(task);
                    if (risk.Level == "High")
                        highRiskCount++;
                }

                if (highRiskCount > 0)
                {
                    riskFactors.Add($"{highRiskCount} high-risk tasks detected");
                }
            }

            // 4. Overloaded employees check -> -10
            var overloadedEmployeeCount = await GetOverloadedEmployeeCountAsync();
            if (overloadedEmployeeCount > 0)
            {
                score -= OverloadedDeduction;
                riskFactors.Add($"{overloadedEmployeeCount} overloaded employee(s)");
            }

            // 5. Projects behind schedule -> -15
            var behindScheduleCount = await GetBehindScheduleProjectCountAsync();
            if (behindScheduleCount > 0)
            {
                score -= BehindScheduleDeduction;
                riskFactors.Add($"{behindScheduleCount} project(s) behind schedule");
            }

            score = Math.Clamp(score, 0, 100);

            var status = ClassifyHealth(score);

            if (status != "Healthy")
            {
                _logger.LogWarning("Team health score: {Score} ({Status}). Risk factors: {Factors}",
                    score, status, string.Join("; ", riskFactors));
            }

            return new TeamHealthScoreResult
            {
                Score = score,
                Status = status,
                RiskFactors = riskFactors
            };
        }

        private async Task<int> GetOverloadedEmployeeCountAsync()
        {
            // Query workload per employee across all active projects
            var workloads = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.Status != TaskStatus.Completed
                         && t.Status != TaskStatus.Cancelled
                         && t.AssignedToEmployeeId.HasValue)
                .GroupBy(t => t.AssignedToEmployeeId!.Value)
                .Select(g => new
                {
                    ActiveTasks = g.Count(),
                    RemainingHours = (int)g.Sum(x =>
                        (x.EstimatedHours ?? 0m) - x.ActualHours > 0
                            ? (x.EstimatedHours ?? 0m) - x.ActualHours
                            : 0m)
                })
                .ToListAsync();

            return workloads.Count(w =>
                Math.Clamp((w.ActiveTasks * 5) + (w.RemainingHours / 2), 0, 100) > OverloadedLoadScoreThreshold);
        }

        private async Task<int> GetBehindScheduleProjectCountAsync()
        {
            var activeProjects = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Status != ProjectStatus.Completed
                         && p.Status != ProjectStatus.Cancelled
                         && !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync();

            var behindCount = 0;
            foreach (var projectId in activeProjects)
            {
                var forecast = await _forecastService.ForecastAsync(projectId);
                if (forecast != null && forecast.Status == "Behind")
                    behindCount++;
            }

            return behindCount;
        }

        internal static string ClassifyHealth(int score)
        {
            return score switch
            {
                >= 80 => "Healthy",
                >= 60 => "Attention",
                _ => "At Risk"
            };
        }
    }
}
