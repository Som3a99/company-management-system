namespace ERP.DAL.Models
{
    public class AppNotification
    {
        public int Id { get; set; }

        // ── Recipient ─────────────────────────────────────────
        public string RecipientUserId { get; set; } = string.Empty;
        public ApplicationUser RecipientUser { get; set; } = null!;

        // ── Content ───────────────────────────────────────────
        /// <summary>Short label shown in the bell dropdown header.
        /// e.g. "Task Assigned" / "Report Ready" / "Security Alert"</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Full descriptive message.
        /// e.g. "API Integration task has been assigned to you by Mona."</summary>
        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; }

        /// <summary>Optional deep-link to the relevant page.</summary>
        public string? LinkUrl { get; set; }

        // ── Severity ──────────────────────────────────────────
        /// <summary>Drives icon colour and ordering in the bell dropdown.
        /// Info (default) | Warning | Critical</summary>
        public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

        // ── State ─────────────────────────────────────────────
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }

        // ── Archive / Soft Delete ─────────────────────────────
        /// <summary>Set to true by the auto-archive job after 90 days.
        /// Archived notifications are excluded from all live queries.
        /// Retained in DB for audit trail purposes.</summary>
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }

        // ── Source ────────────────────────────────────────────
        /// <summary>True when the notification was created by a background
        /// job or system service (not a direct user action).
        /// Useful for future admin-broadcast messages.</summary>
        public bool IsSystemGenerated { get; set; } = false;
    }
}
