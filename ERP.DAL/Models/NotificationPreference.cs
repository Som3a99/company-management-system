namespace ERP.DAL.Models
{
    /// <summary>
    /// Per-user notification preferences.
    /// Controls which notification types are delivered and through which channels.
    /// Critical alerts (severity = Critical) are always delivered regardless of preferences.
    /// </summary>
    public class NotificationPreference
    {
        public int Id { get; set; }

        // ── Owner ─────────────────────────────────────────────
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        // ── In-App Delivery ───────────────────────────────────
        /// <summary>When false, non-critical in-app notifications are suppressed.
        /// Critical alerts are always delivered.</summary>
        public bool InAppEnabled { get; set; } = true;

        // ── Email Delivery ────────────────────────────────────
        /// <summary>When true, the system will attempt to send email for
        /// applicable notification types (e.g. password reset denial).
        /// Default off — users opt-in via settings.</summary>
        public bool EmailEnabled { get; set; } = false;

        // ── Per-Type Muting ───────────────────────────────────
        /// <summary>When true, task assignment notifications (N-01) are suppressed.</summary>
        public bool MuteTaskAssigned { get; set; } = false;

        /// <summary>When true, task status change notifications (N-02) are suppressed.</summary>
        public bool MuteTaskStatusChanged { get; set; } = false;

        /// <summary>When true, report ready/failed notifications (N-05/N-06) are suppressed.</summary>
        public bool MuteReportNotifications { get; set; } = false;

        // ── Timestamps ────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
