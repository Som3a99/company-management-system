namespace ERP.DAL.Models
{
    /// <summary>
    /// Password reset request status
    /// </summary>
    public enum ResetStatus
    {
        Pending = 0,
        Approved = 1,
        Denied = 2,
        Expired = 3,
        Completed = 4
    }

    /// <summary>
    /// Tracks password reset requests with IT approval workflow
    /// </summary>
    public class PasswordResetRequest : Base
    {
        /// <summary>
        /// ApplicationUser.Id who requested the reset
        /// </summary>
        public string UserId { get; set; } = null!;

        /// <summary>
        /// User's email at time of request
        /// </summary>
        public string UserEmail { get; set; } = null!;

        /// <summary>
        /// Unique ticket number for tracking (e.g., RST-2026-001234)
        /// </summary>
        public string TicketNumber { get; set; } = null!;

        /// <summary>
        /// Request status
        /// </summary>
        public ResetStatus Status { get; set; }

        /// <summary>
        /// When the request was created
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Request expires after 1 hour if not processed
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// When the request was resolved (approved/denied)
        /// </summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Who resolved the request (IT Admin email)
        /// </summary>
        public string? ResolvedBy { get; set; }

        /// <summary>
        /// Reason for denial (if applicable)
        /// </summary>
        public string? DenialReason { get; set; }

        /// <summary>
        /// IP address of requester
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent of requester
        /// </summary>
        public string? UserAgent { get; set; }
    }
}
