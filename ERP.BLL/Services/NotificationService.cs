using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.BLL.Services
{
    public sealed class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<NotificationService> _logger;
        private readonly INotificationHubService? _hubService;

        public NotificationService(
            ApplicationDbContext dbContext,
            ILogger<NotificationService> logger,
            INotificationHubService? hubService = null)
        {
            _dbContext = dbContext;
            _logger = logger;
            _hubService = hubService;
        }

        public async Task CreateAsync(
            string recipientUserId,
            string title,
            string message,
            NotificationType type,
            NotificationSeverity severity = NotificationSeverity.Info,
            string? linkUrl = null,
            bool isSystemGenerated = false)
        {
            if (string.IsNullOrWhiteSpace(recipientUserId))
                return;

            // Check user preferences — critical alerts always bypass
            if (severity != NotificationSeverity.Critical
                && await IsNotificationMutedAsync(recipientUserId, type))
            {
                return;
            }

            var notification = new AppNotification
            {
                RecipientUserId = recipientUserId,
                Title = Truncate(title, 120),
                Message = Truncate(message, 500),
                Type = type,
                Severity = severity,
                LinkUrl = linkUrl != null ? Truncate(linkUrl, 300) : null,
                IsSystemGenerated = isSystemGenerated,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();

                // Push real-time update if SignalR is available
                await PushNotificationAsync(notification);
            }
            catch (Exception ex)
            {
                // Notifications must never break business operations
                _logger.LogError(ex, "Failed to create notification for user {UserId}: {Title}", recipientUserId, title);
            }
        }

        public async Task CreateForManyAsync(
            IEnumerable<string> recipientUserIds,
            string title,
            string message,
            NotificationType type,
            NotificationSeverity severity = NotificationSeverity.Info,
            string? linkUrl = null,
            bool isSystemGenerated = false)
        {
            var userIds = recipientUserIds?.Distinct().Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (userIds == null || userIds.Count == 0)
                return;

            // Filter out users who have muted this notification type (critical bypasses)
            if (severity != NotificationSeverity.Critical)
            {
                var filteredIds = new List<string>();
                foreach (var uid in userIds)
                {
                    if (!await IsNotificationMutedAsync(uid, type))
                        filteredIds.Add(uid);
                }
                userIds = filteredIds;
                if (userIds.Count == 0) return;
            }

            var now = DateTime.UtcNow;
            var truncatedTitle = Truncate(title, 120);
            var truncatedMessage = Truncate(message, 500);
            var truncatedLink = linkUrl != null ? Truncate(linkUrl, 300) : null;

            var notifications = userIds.Select(userId => new AppNotification
            {
                RecipientUserId = userId,
                Title = truncatedTitle,
                Message = truncatedMessage,
                Type = type,
                Severity = severity,
                LinkUrl = truncatedLink,
                IsSystemGenerated = isSystemGenerated,
                CreatedAt = now
            }).ToList();

            try
            {
                _dbContext.Notifications.AddRange(notifications);
                await _dbContext.SaveChangesAsync();

                // Push real-time updates for each recipient
                foreach (var n in notifications)
                {
                    await PushNotificationAsync(n);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create {Count} notifications: {Title}", notifications.Count, title);
            }
        }

        public async Task<List<AppNotification>> GetForUserAsync(
            string userId,
            int take = 20,
            bool includeArchived = false)
        {
            IQueryable<AppNotification> query = _dbContext.Notifications;

            if (includeArchived)
            {
                query = query.IgnoreQueryFilters();
            }

            return await query
                .Where(n => n.RecipientUserId == userId)
                .OrderByDescending(n => n.Severity)
                .ThenByDescending(n => n.CreatedAt)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _dbContext.Notifications
                .Where(n => n.RecipientUserId == userId && !n.IsRead)
                .CountAsync();
        }

        public async Task MarkReadAsync(int notificationId, string userId)
        {
            try
            {
                var notification = await _dbContext.Notifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId);

                if (notification != null && !notification.IsRead)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark notification {NotificationId} as read for user {UserId}", notificationId, userId);
            }
        }

        public async Task MarkAllReadAsync(string userId)
        {
            try
            {
                var unreadNotifications = await _dbContext.Notifications
                    .Where(n => n.RecipientUserId == userId && !n.IsRead)
                    .ToListAsync();

                if (unreadNotifications.Count == 0)
                    return;

                var now = DateTime.UtcNow;
                foreach (var n in unreadNotifications)
                {
                    n.IsRead = true;
                    n.ReadAt = now;
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark all notifications as read for user {UserId}", userId);
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            return string.IsNullOrEmpty(value) ? value : value.Length <= maxLength ? value : value[..maxLength];
        }

        /// <summary>
        /// Check whether a user has muted a particular notification type via their preferences.
        /// Returns false (not muted) when no preferences record exists (defaults are all enabled).
        /// </summary>
        private async Task<bool> IsNotificationMutedAsync(string userId, NotificationType type)
        {
            try
            {
                var pref = await _dbContext.NotificationPreferences
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (pref == null) return false; // No prefs → all enabled (defaults)

                // Global in-app toggle
                if (!pref.InAppEnabled) return true;

                // Per-type muting
                return type switch
                {
                    NotificationType.TaskAssigned => pref.MuteTaskAssigned,
                    NotificationType.TaskStatusChanged => pref.MuteTaskStatusChanged,
                    NotificationType.ReportReady or NotificationType.ReportFailed => pref.MuteReportNotifications,
                    _ => false // All other types cannot be muted
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check preferences for user {UserId}, allowing notification", userId);
                return false; // Fail open — deliver the notification
            }
        }

        /// <summary>
        /// Push real-time notification + updated unread count to the recipient via SignalR.
        /// Gracefully no-ops if hub service is unavailable or push fails.
        /// </summary>
        private async Task PushNotificationAsync(AppNotification notification)
        {
            if (_hubService == null) return;

            try
            {
                var payload = new
                {
                    notification.Id,
                    notification.Title,
                    Message = notification.Message.Length > 80
                        ? notification.Message[..80] + "…"
                        : notification.Message,
                    notification.LinkUrl,
                    notification.IsRead,
                    Severity = notification.Severity.ToString().ToLower(),
                    notification.CreatedAt
                };

                await _hubService.SendNotificationAsync(notification.RecipientUserId, payload);

                // Also push the new unread count
                var count = await _dbContext.Notifications
                    .CountAsync(n => n.RecipientUserId == notification.RecipientUserId && !n.IsRead);

                await _hubService.SendUnreadCountAsync(notification.RecipientUserId, count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push real-time notification {NotifId} to user {UserId}",
                    notification.Id, notification.RecipientUserId);
            }
        }

        // ── Broadcast ─────────────────────────────────────────

        public async Task BroadcastAsync(
            string title,
            string message,
            NotificationSeverity severity = NotificationSeverity.Info,
            string? linkUrl = null)
        {
            try
            {
                // Get all active user IDs
                var activeUserIds = await _dbContext.Users
                    .Where(u => u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();

                if (activeUserIds.Count == 0) return;

                // Broadcast bypasses preferences (system-wide announcement)
                var now = DateTime.UtcNow;
                var truncatedTitle = Truncate(title, 120);
                var truncatedMessage = Truncate(message, 500);
                var truncatedLink = linkUrl != null ? Truncate(linkUrl, 300) : null;

                var notifications = activeUserIds.Select(userId => new AppNotification
                {
                    RecipientUserId = userId,
                    Title = truncatedTitle,
                    Message = truncatedMessage,
                    Type = NotificationType.TaskAssigned, // System use — no dedicated enum yet
                    Severity = severity,
                    LinkUrl = truncatedLink,
                    IsSystemGenerated = true,
                    CreatedAt = now
                }).ToList();

                _dbContext.Notifications.AddRange(notifications);
                await _dbContext.SaveChangesAsync();

                // Push real-time updates
                foreach (var n in notifications)
                {
                    await PushNotificationAsync(n);
                }

                _logger.LogInformation("Broadcast notification sent to {Count} users: {Title}", activeUserIds.Count, title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast notification: {Title}", title);
            }
        }

        // ── Preferences ───────────────────────────────────────

        public async Task<NotificationPreference> GetPreferencesAsync(string userId)
        {
            var pref = await _dbContext.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (pref != null) return pref;

            // Create default preferences
            pref = new NotificationPreference
            {
                UserId = userId,
                InAppEnabled = true,
                EmailEnabled = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.NotificationPreferences.Add(pref);
            await _dbContext.SaveChangesAsync();
            return pref;
        }

        public async Task UpdatePreferencesAsync(NotificationPreference preferences)
        {
            try
            {
                var existing = await _dbContext.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == preferences.UserId);

                if (existing == null)
                {
                    _dbContext.NotificationPreferences.Add(preferences);
                }
                else
                {
                    existing.InAppEnabled = preferences.InAppEnabled;
                    existing.EmailEnabled = preferences.EmailEnabled;
                    existing.MuteTaskAssigned = preferences.MuteTaskAssigned;
                    existing.MuteTaskStatusChanged = preferences.MuteTaskStatusChanged;
                    existing.MuteReportNotifications = preferences.MuteReportNotifications;
                    existing.UpdatedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update notification preferences for user {UserId}", preferences.UserId);
            }
        }
    }
}
