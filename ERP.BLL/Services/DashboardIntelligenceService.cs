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
    public class DashboardIntelligenceService : IDashboardIntelligenceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITaskRiskService _taskRiskService;
        private readonly ITeamHealthService _teamHealthService;
        private readonly IAuditAnomalyService _anomalyService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DashboardIntelligenceService> _logger;

        public DashboardIntelligenceService(
            ApplicationDbContext context,
            ITaskRiskService taskRiskService,
            ITeamHealthService teamHealthService,
            IAuditAnomalyService anomalyService,
            IMemoryCache cache,
            ILogger<DashboardIntelligenceService> logger)
        {
            _context = context;
            _taskRiskService = taskRiskService;
            _teamHealthService = teamHealthService;
            _anomalyService = anomalyService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<DashboardIntelligenceData> GetIntelligenceAsync()
        {
            var cacheKey = "erp:dashboard:intelligence";

            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.Size = 1;
                return await ComputeIntelligenceAsync();
            });

            return result ?? new DashboardIntelligenceData();
        }

        private async Task<DashboardIntelligenceData> ComputeIntelligenceAsync()
        {
            // 1. High risk task count
            var activeTasks = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled)
                .ToListAsync();

            var highRiskCount = 0;
            foreach (var task in activeTasks)
            {
                var risk = _taskRiskService.CalculateRisk(task);
                if (risk.Level == "High")
                    highRiskCount++;
            }

            // 2. Overloaded employees count
            var overloadedCount = await GetOverloadedEmployeeCountAsync();

            // 3. Behind-schedule projects count
            var behindCount = await GetBehindScheduleProjectCountAsync();

            // 4. Team health
            var teamHealth = await _teamHealthService.CalculateAsync();

            // 5. Anomaly count
            var anomalies = await _anomalyService.DetectAnomaliesAsync(24);
            var anomalyCount = anomalies.Count;

            return new DashboardIntelligenceData
            {
                HighRiskTaskCount = highRiskCount,
                OverloadedEmployeeCount = overloadedCount,
                BehindScheduleProjectCount = behindCount,
                TeamHealth = teamHealth,
                AnomalyCount = anomalyCount
            };
        }

        private async Task<int> GetOverloadedEmployeeCountAsync()
        {
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
                Math.Clamp((w.ActiveTasks * 5) + (w.RemainingHours / 2), 0, 100) > 70);
        }

        private async Task<int> GetBehindScheduleProjectCountAsync()
        {
            // Count projects whose EndDate is in the past and are still active
            return await _context.Projects
                .AsNoTracking()
                .Where(p => p.Status != ProjectStatus.Completed
                         && p.Status != ProjectStatus.Cancelled
                         && !p.IsDeleted
                         && p.EndDate.HasValue
                         && p.EndDate.Value.Date < DateTime.UtcNow.Date)
                .CountAsync();
        }
    }
}
