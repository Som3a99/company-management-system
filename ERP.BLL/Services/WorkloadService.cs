using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ERP.BLL.Services
{
    public class WorkloadService : IWorkloadService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<WorkloadService> _logger;

        public WorkloadService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<WorkloadService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<EmployeeWorkloadResult>> GetWorkloadAsync(int projectId)
        {
            var cacheKey = $"workload_{projectId}";

            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.Size = 1;
                return await ComputeWorkloadAsync(projectId);
            });

            return result ?? new List<EmployeeWorkloadResult>();
        }

        private async Task<List<EmployeeWorkloadResult>> ComputeWorkloadAsync(int projectId)
        {
            // Optimized EF query with projection — avoids N+1
            var workloads = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.Status != DAL.Models.TaskStatus.Completed
                         && t.Status != DAL.Models.TaskStatus.Cancelled
                         && t.ProjectId == projectId
                         && t.AssignedToEmployeeId.HasValue)
                .GroupBy(t => new
                {
                    EmployeeId = t.AssignedToEmployeeId!.Value,
                    EmployeeName = t.AssignedToEmployee.FirstName + " " + t.AssignedToEmployee.LastName
                })
                .Select(g => new EmployeeWorkloadResult
                {
                    EmployeeId = g.Key.EmployeeId,
                    EmployeeName = g.Key.EmployeeName,
                    ActiveTasks = g.Count(),
                    RemainingHours = (int)g.Sum(x =>
                        (x.EstimatedHours ?? 0m) - x.ActualHours > 0
                            ? (x.EstimatedHours ?? 0m) - x.ActualHours
                            : 0m)
                })
                .ToListAsync();

            // Compute LoadScore and Label for each result
            foreach (var w in workloads)
            {
                w.LoadScore = Math.Clamp((w.ActiveTasks * 5) + (w.RemainingHours / 2), 0, 100);

                w.Label = w.LoadScore switch
                {
                    <= 30 => "Available",
                    <= 70 => "Moderate",
                    _ => "Heavy"
                };

                // Log overload situations
                if (w.LoadScore > 70)
                {
                    _logger.LogWarning(
                        "Employee overload risk: {EmployeeId} ({EmployeeName}), LoadScore: {LoadScore}, ActiveTasks: {ActiveTasks}, RemainingHours: {RemainingHours}",
                        w.EmployeeId, w.EmployeeName, w.LoadScore, w.ActiveTasks, w.RemainingHours);
                }
            }

            return workloads.OrderBy(w => w.LoadScore).ToList();
        }
    }
}
