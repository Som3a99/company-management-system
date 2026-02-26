using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ERP.BLL.Services
{
    /// <summary>
    /// Lightweight projection used inside anomaly detection so that
    /// the static rule methods can work without anonymous types.
    /// </summary>
    internal sealed class AuditLogEntry
    {
        public string UserId { get; init; } = string.Empty;
        public string UserEmail { get; init; } = string.Empty;
        public string Action { get; init; } = string.Empty;
        public bool Succeeded { get; init; }
        public DateTime Timestamp { get; init; }
    }

    public class AuditAnomalyService : IAuditAnomalyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuditAnomalyService> _logger;

        // Configurable thresholds
        internal const int ActivitySpikeThreshold = 10;
        internal const int ActivitySpikeWindowMinutes = 5;
        internal const int FailedAttemptThreshold = 3;
        internal const int DestructiveActionThreshold = 3;
        internal const int OffHoursStart = 23; // 11 PM
        internal const int OffHoursEnd = 5;    // 5 AM

        internal static readonly HashSet<string> DestructiveActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "DELETE_EMPLOYEE", "DELETE_DEPARTMENT", "DELETE_PROJECT",
            "DELETE_TASK", "REMOVE_MEMBER", "DEACTIVATE_USER",
            "LOCK_ACCOUNT", "RESET_PASSWORD"
        };

        public AuditAnomalyService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<AuditAnomalyService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<AuditAnomalyFlag>> DetectAnomaliesAsync(int lookbackHours = 24)
        {
            var cacheKey = $"erp:anomaly:{lookbackHours}";

            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await DetectAnomaliesCoreAsync(lookbackHours);
            });

            return result ?? new List<AuditAnomalyFlag>();
        }

        private async Task<List<AuditAnomalyFlag>> DetectAnomaliesCoreAsync(int lookbackHours)
        {
            var cutoff = DateTime.UtcNow.AddHours(-lookbackHours);
            var logs = await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.Timestamp >= cutoff)
                .OrderBy(a => a.Timestamp)
                .Select(a => new AuditLogEntry
                {
                    UserId = a.UserId,
                    UserEmail = a.UserEmail,
                    Action = a.Action,
                    Succeeded = a.Succeeded,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();

            if (logs.Count == 0)
                return new List<AuditAnomalyFlag>();

            var anomalies = new List<AuditAnomalyFlag>();

            // Group by user for per-user anomaly detection
            var userGroups = logs.GroupBy(l => l.UserId).ToList();

            foreach (var group in userGroups)
            {
                var userId = group.Key;
                var userEmail = group.First().UserEmail;
                var userLogs = group.OrderBy(l => l.Timestamp).ToList();

                // 1. Activity Spike: 10+ actions within 5 minutes
                DetectActivitySpikes(userId, userEmail, userLogs, anomalies);

                // 2. Off-Hours Activity: actions between 11PM-5AM
                DetectOffHoursActivity(userId, userEmail, userLogs, anomalies);

                // 3. Failed Attempts: 3+ consecutive failures
                DetectFailedAttempts(userId, userEmail, userLogs, anomalies);

                // 4. Destructive Patterns: multiple deletes/destructive actions
                DetectDestructivePatterns(userId, userEmail, userLogs, anomalies);
            }

            foreach (var anomaly in anomalies)
            {
                _logger.LogWarning(
                    "Audit anomaly detected: {AnomalyType} for user {UserEmail} — {Description}",
                    anomaly.AnomalyType, anomaly.UserEmail, anomaly.Description);
            }

            return anomalies
                .OrderByDescending(a => a.Severity == "High")
                .ThenByDescending(a => a.Severity == "Medium")
                .ThenByDescending(a => a.DetectedAt)
                .ToList();
        }

        internal static void DetectActivitySpikes(
            string userId, string userEmail,
            List<AuditLogEntry> userLogs,
            List<AuditAnomalyFlag> anomalies)
        {
            for (int i = 0; i < userLogs.Count; i++)
            {
                var windowEnd = userLogs[i].Timestamp.AddMinutes(ActivitySpikeWindowMinutes);
                var windowCount = 0;

                for (int j = i; j < userLogs.Count && userLogs[j].Timestamp <= windowEnd; j++)
                {
                    windowCount++;
                }

                if (windowCount >= ActivitySpikeThreshold)
                {
                    anomalies.Add(new AuditAnomalyFlag
                    {
                        UserId = userId,
                        UserEmail = userEmail,
                        AnomalyType = "ActivitySpike",
                        Description = $"{windowCount} actions within {ActivitySpikeWindowMinutes} minutes",
                        Severity = windowCount >= 20 ? "High" : "Medium",
                        DetectedAt = userLogs[i].Timestamp,
                        RelatedLogCount = windowCount
                    });

                    // Skip ahead to avoid duplicate detections for same window
                    break;
                }
            }
        }

        internal static void DetectOffHoursActivity(
            string userId, string userEmail,
            List<AuditLogEntry> userLogs,
            List<AuditAnomalyFlag> anomalies)
        {
            var offHoursLogs = userLogs
                .Where(l => IsOffHours(l.Timestamp))
                .ToList();

            if (offHoursLogs.Count > 0)
            {
                anomalies.Add(new AuditAnomalyFlag
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    AnomalyType = "OffHoursAccess",
                    Description = $"{offHoursLogs.Count} action(s) between {OffHoursStart}:00-{OffHoursEnd}:00",
                    Severity = offHoursLogs.Count >= 5 ? "High" : "Low",
                    DetectedAt = offHoursLogs.First().Timestamp,
                    RelatedLogCount = offHoursLogs.Count
                });
            }
        }

        internal static void DetectFailedAttempts(
            string userId, string userEmail,
            List<AuditLogEntry> userLogs,
            List<AuditAnomalyFlag> anomalies)
        {
            var consecutiveFailures = 0;
            var maxConsecutive = 0;

            foreach (var log in userLogs)
            {
                if (!log.Succeeded)
                {
                    consecutiveFailures++;
                    maxConsecutive = Math.Max(maxConsecutive, consecutiveFailures);
                }
                else
                {
                    consecutiveFailures = 0;
                }
            }

            if (maxConsecutive >= FailedAttemptThreshold)
            {
                anomalies.Add(new AuditAnomalyFlag
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    AnomalyType = "ConsecutiveFailures",
                    Description = $"{maxConsecutive} consecutive failed attempts",
                    Severity = maxConsecutive >= 5 ? "High" : "Medium",
                    DetectedAt = DateTime.UtcNow,
                    RelatedLogCount = maxConsecutive
                });
            }
        }

        internal static void DetectDestructivePatterns(
            string userId, string userEmail,
            List<AuditLogEntry> userLogs,
            List<AuditAnomalyFlag> anomalies)
        {
            var destructiveCount = userLogs
                .Count(l => DestructiveActions.Contains(l.Action));

            if (destructiveCount >= DestructiveActionThreshold)
            {
                anomalies.Add(new AuditAnomalyFlag
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    AnomalyType = "DestructivePattern",
                    Description = $"{destructiveCount} destructive actions detected",
                    Severity = destructiveCount >= 5 ? "High" : "Medium",
                    DetectedAt = DateTime.UtcNow,
                    RelatedLogCount = destructiveCount
                });
            }
        }

        internal static bool IsOffHours(DateTime timestamp)
        {
            var hour = timestamp.Hour;
            return hour >= OffHoursStart || hour < OffHoursEnd;
        }
    }
}
