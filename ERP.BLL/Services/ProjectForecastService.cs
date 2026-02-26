using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskStatus = ERP.DAL.Models.TaskStatus;
using ProjectStatus = ERP.DAL.Models.ProjectStatus;

namespace ERP.BLL.Services
{
    public sealed class ProjectForecastService : IProjectForecastService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ProjectForecastService> _logger;

        private const string CachePrefix = "erp:forecast:project:";

        public ProjectForecastService(
            ApplicationDbContext dbContext,
            ICacheService cacheService,
            ILogger<ProjectForecastService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<ProjectForecastResult?> ForecastAsync(int projectId)
        {
            var cacheKey = $"{CachePrefix}{projectId}";

            return await _cacheService.GetOrCreateNullableAsync(
                cacheKey,
                async () => await ForecastCoreAsync(projectId),
                TimeSpan.FromHours(1));
        }

        private async Task<ProjectForecastResult?> ForecastCoreAsync(int projectId)
        {
            var project = await _dbContext.Projects
                .AsNoTracking()
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return null;

            // Only forecast active projects
            if (project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.Cancelled)
                return null;

            var allTasks = project.Tasks.ToList();
            if (!allTasks.Any())
                return null;

            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);

            var completedLast30Days = allTasks
                .Count(t => t.Status == TaskStatus.Completed && t.CompletedAt.HasValue && t.CompletedAt.Value >= thirtyDaysAgo);

            var remainingTasks = allTasks
                .Count(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled);

            // Velocity = tasks completed per day over last 30 days
            var velocity = completedLast30Days / 30.0;

            DateTime estimatedCompletion;
            if (velocity <= 0)
            {
                // Zero velocity: cannot estimate, project at 180 days out as placeholder
                estimatedCompletion = now.AddDays(180);
            }
            else
            {
                var daysToComplete = (int)Math.Ceiling(remainingTasks / velocity);
                estimatedCompletion = now.AddDays(daysToComplete);
            }

            // Calculate days behind schedule based on project end date
            var daysBehind = 0;
            if (project.EndDate.HasValue)
            {
                daysBehind = (int)(estimatedCompletion - project.EndDate.Value).TotalDays;
                if (daysBehind < 0) daysBehind = 0;
            }

            // Determine status
            var status = DetermineStatus(daysBehind, velocity, remainingTasks, project.EndDate);

            return new ProjectForecastResult
            {
                EstimatedCompletionDate = estimatedCompletion.Date,
                DaysBehindSchedule = daysBehind,
                Status = status,
                Velocity = Math.Round(velocity, 2),
                RemainingTasks = remainingTasks,
                CompletedTasksLast30Days = completedLast30Days
            };
        }

        internal static string DetermineStatus(int daysBehind, double velocity, int remainingTasks, DateTime? endDate)
        {
            if (remainingTasks == 0)
                return "On Track";

            if (velocity <= 0)
                return "Behind";

            if (!endDate.HasValue)
                return "On Track"; // No deadline to be behind on

            if (daysBehind <= 0)
                return "On Track";

            if (daysBehind <= 5)
                return "At Risk";

            return "Behind";
        }
    }
}
