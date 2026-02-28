using ERP.BLL.Interfaces;
using ERP.PL.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ERP.PL.Services
{
    /// <summary>
    /// SignalR-backed implementation of INotificationHubService.
    /// Pushes real-time events to connected clients via the NotificationHub.
    /// All calls are fire-and-forget safe — failures are logged, never thrown.
    /// </summary>
    public sealed class SignalRNotificationHubService : INotificationHubService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SignalRNotificationHubService> _logger;

        public SignalRNotificationHubService(
            IHubContext<NotificationHub> hubContext,
            ILogger<SignalRNotificationHubService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task SendNotificationAsync(string userId, object notification)
        {
            try
            {
                await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", notification);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push notification to user {UserId} via SignalR", userId);
            }
        }

        public async Task SendUnreadCountAsync(string userId, int count)
        {
            try
            {
                await _hubContext.Clients.User(userId).SendAsync("UpdateUnreadCount", count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push unread count to user {UserId} via SignalR", userId);
            }
        }

        public async Task SendUnreadCountAsync(IEnumerable<string> userIds, int count)
        {
            try
            {
                var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
                if (ids.Count == 0) return;
                await _hubContext.Clients.Users(ids).SendAsync("UpdateUnreadCount", count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push unread count to multiple users via SignalR");
            }
        }
    }
}
