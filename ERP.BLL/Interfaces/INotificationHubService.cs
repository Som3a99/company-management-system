namespace ERP.BLL.Interfaces
{
    /// <summary>
    /// Abstraction for pushing real-time notification events to connected clients.
    /// Implemented in the PL layer using SignalR. The BLL depends only on this interface,
    /// keeping the SignalR dependency out of the business layer.
    /// </summary>
    public interface INotificationHubService
    {
        /// <summary>
        /// Push a new notification payload to a specific user's connected clients.
        /// </summary>
        Task SendNotificationAsync(string userId, object notification);

        /// <summary>
        /// Push an updated unread count to a specific user's connected clients.
        /// </summary>
        Task SendUnreadCountAsync(string userId, int count);

        /// <summary>
        /// Push an updated unread count to multiple users simultaneously.
        /// </summary>
        Task SendUnreadCountAsync(IEnumerable<string> userIds, int count);
    }
}
