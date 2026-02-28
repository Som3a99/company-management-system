using ERP.DAL.Data.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Services
{
    /// <summary>
    /// Background job that permanently deletes archived notifications older than 1 year.
    /// This prevents unbounded table growth while respecting the archive lifecycle:
    ///   1. Notifications are created → live for 90-180 days
    ///   2. NotificationArchiveJob sets IsArchived = true (soft archive)
    ///   3. This job hard-deletes archived records after 365 days
    /// 
    /// Runs once daily. Uses batched deletes to avoid lock contention.
    /// </summary>
    public sealed class NotificationHardDeleteJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationHardDeleteJob> _logger;
        private const int BatchSize = 500;

        public NotificationHardDeleteJob(
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationHardDeleteJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Delay startup to avoid contention with other jobs
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PurgeOldArchivedNotificationsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex, "NotificationHardDeleteJob failed");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task PurgeOldArchivedNotificationsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-365);
            int totalDeleted = 0;

            // Batch delete to avoid long-running transactions
            while (!ct.IsCancellationRequested)
            {
                var batch = await db.Notifications
                    .IgnoreQueryFilters()
                    .Where(n => n.IsArchived && n.ArchivedAt.HasValue && n.ArchivedAt.Value < cutoff)
                    .OrderBy(n => n.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0)
                    break;

                db.Notifications.RemoveRange(batch);
                await db.SaveChangesAsync(ct);
                totalDeleted += batch.Count;
            }

            if (totalDeleted > 0)
            {
                _logger.LogInformation(
                    "NotificationHardDeleteJob: permanently deleted {Count} archived notifications older than 1 year.",
                    totalDeleted);
            }
        }
    }
}
