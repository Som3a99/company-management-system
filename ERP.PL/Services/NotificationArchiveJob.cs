using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Services
{
    /// <summary>
    /// Background job that archives old read notifications.
    /// Standard notifications: archived after 90 days.
    /// Critical notifications: archived after 180 days.
    /// Only archives notifications that have been read.
    /// Runs once daily.
    /// </summary>
    public sealed class NotificationArchiveJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationArchiveJob> _logger;

        public NotificationArchiveJob(
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationArchiveJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Run once daily
                try
                {
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var standardCutoff = DateTime.UtcNow.AddDays(-90);
                    var criticalCutoff = DateTime.UtcNow.AddDays(-180);

                    // Must use IgnoreQueryFilters since the global filter excludes IsArchived = true,
                    // but we're querying for non-archived ones to archive them.
                    var toArchive = await db.Notifications
                        .IgnoreQueryFilters()
                        .Where(n => !n.IsArchived
                                 && n.IsRead
                                 && n.Severity != NotificationSeverity.Critical
                                 && n.CreatedAt < standardCutoff)
                        .ToListAsync(stoppingToken);

                    var criticalToArchive = await db.Notifications
                        .IgnoreQueryFilters()
                        .Where(n => !n.IsArchived
                                 && n.IsRead
                                 && n.Severity == NotificationSeverity.Critical
                                 && n.CreatedAt < criticalCutoff)
                        .ToListAsync(stoppingToken);

                    var now = DateTime.UtcNow;
                    foreach (var n in toArchive.Concat(criticalToArchive))
                    {
                        n.IsArchived = true;
                        n.ArchivedAt = now;
                    }

                    await db.SaveChangesAsync(stoppingToken);

                    var totalArchived = toArchive.Count + criticalToArchive.Count;
                    if (totalArchived > 0)
                    {
                        _logger.LogInformation(
                            "NotificationArchiveJob: archived {Count} notifications ({Standard} standard, {Critical} critical).",
                            totalArchived, toArchive.Count, criticalToArchive.Count);
                    }
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex, "NotificationArchiveJob failed.");
                }
            }
        }
    }
}
