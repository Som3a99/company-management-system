using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ERP.PL.Hubs
{
    /// <summary>
    /// SignalR hub for real-time notification delivery.
    /// Users are automatically grouped by their authenticated user ID.
    /// 
    /// Server → Client methods:
    ///   ReceiveNotification(object notification) — pushed when a new notification is created
    ///   UpdateUnreadCount(int count)             — pushed when unread count changes
    /// 
    /// The hub itself has no client → server methods.
    /// All notification creation flows through INotificationService, which triggers
    /// hub pushes via INotificationHubService (decoupled from SignalR dependency).
    /// </summary>
    [Authorize]
    public sealed class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            _logger.LogDebug("NotificationHub: user {UserId} connected (ConnectionId: {ConnectionId})", userId, Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            _logger.LogDebug("NotificationHub: user {UserId} disconnected (ConnectionId: {ConnectionId})", userId, Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
