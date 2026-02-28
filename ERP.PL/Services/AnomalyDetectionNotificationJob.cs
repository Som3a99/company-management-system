using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Services
{
    /// <summary>
    /// Background job that runs the audit anomaly scanner periodically
    /// and sends N-07 Critical notifications to CEOs when anomalies are detected.
    /// Runs every 2 hours.
    /// </summary>
    public sealed class AnomalyDetectionNotificationJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AnomalyDetectionNotificationJob> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(2);

        public AnomalyDetectionNotificationJob(
            IServiceScopeFactory scopeFactory,
            ILogger<AnomalyDetectionNotificationJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait for app to fully initialize
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DetectAndNotifyAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex, "AnomalyDetectionNotificationJob failed");
                }

                try
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task DetectAndNotifyAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var anomalyService = scope.ServiceProvider.GetRequiredService<IAuditAnomalyService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Detect anomalies from the last 2 hours
            var anomalies = await anomalyService.DetectAnomaliesAsync(lookbackHours: 2);

            // Only notify for High severity anomalies
            var highAnomalies = anomalies.Where(a => a.Severity == "High").ToList();
            if (highAnomalies.Count == 0)
                return;

            // Find CEO user IDs
            var ceoRoleId = await db.Roles
                .Where(r => r.Name == "CEO")
                .Select(r => r.Id)
                .FirstOrDefaultAsync(ct);

            if (ceoRoleId == null)
                return;

            var ceoUserIds = await db.UserRoles
                .Where(ur => ur.RoleId == ceoRoleId)
                .Select(ur => ur.UserId)
                .ToListAsync(ct);

            if (ceoUserIds.Count == 0)
                return;

            // Check for duplicate notifications to avoid alerting on the same anomaly twice
            var today = DateTime.UtcNow.Date;
            var existingAnomalyMessages = await db.Notifications
                .IgnoreQueryFilters()
                .Where(n => n.Type == NotificationType.AnomalyDetected && n.CreatedAt >= today)
                .Select(n => n.Message)
                .ToListAsync(ct);
            var existingSet = new HashSet<string>(existingAnomalyMessages);

            foreach (var anomaly in highAnomalies)
            {
                var message = $"Security anomaly detected: {anomaly.Description}. "
                            + $"User: {anomaly.UserEmail ?? "Unknown"}, "
                            + $"Time: {anomaly.DetectedAt:HH:mm UTC}.";

                if (existingSet.Contains(message))
                    continue;

                await notificationService.CreateForManyAsync(
                    ceoUserIds,
                    title: "Security Anomaly",
                    message: message,
                    type: NotificationType.AnomalyDetected,
                    severity: NotificationSeverity.Critical,
                    linkUrl: "/Reporting?reportType=Audit",
                    isSystemGenerated: true);

                existingSet.Add(message);
            }

            _logger.LogInformation(
                "AnomalyDetectionNotificationJob: {Count} high-severity anomalies detected and notified.",
                highAnomalies.Count);
        }
    }
}
