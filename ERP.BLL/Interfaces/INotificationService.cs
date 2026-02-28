using ERP.DAL.Models;

namespace ERP.BLL.Interfaces
{
    public interface INotificationService
    {
        Task CreateAsync(
            string recipientUserId,
            string title,
            string message,
            NotificationType type,
            NotificationSeverity severity = NotificationSeverity.Info,
            string? linkUrl = null,
            bool isSystemGenerated = false);

        Task CreateForManyAsync(
            IEnumerable<string> recipientUserIds,
            string title,
            string message,
            NotificationType type,
            NotificationSeverity severity = NotificationSeverity.Info,
            string? linkUrl = null,
            bool isSystemGenerated = false);

        Task<List<AppNotification>> GetForUserAsync(
            string userId,
            int take = 20,
            bool includeArchived = false);

        Task<int> GetUnreadCountAsync(string userId);

        Task MarkReadAsync(int notificationId, string userId);

        Task MarkAllReadAsync(string userId);

        /// <summary>
        /// Broadcast a system notification to ALL active users (e.g. maintenance alerts).
        /// Always marked as IsSystemGenerated = true. Cannot be muted via preferences.
        /// </summary>
        Task BroadcastAsync(
            string title,
            string message,
            NotificationSeverity severity = NotificationSeverity.Info,
            string? linkUrl = null);

        /// <summary>
        /// Get or create the notification preferences for a user.
        /// Returns default preferences if none exist.
        /// </summary>
        Task<NotificationPreference> GetPreferencesAsync(string userId);

        /// <summary>
        /// Update notification preferences for a user.
        /// </summary>
        Task UpdatePreferencesAsync(NotificationPreference preferences);
    }
}
