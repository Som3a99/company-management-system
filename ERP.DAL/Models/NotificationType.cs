namespace ERP.DAL.Models
{
    public enum NotificationType
    {
        // ── Task & Workflow ───────────────────────────────────
        TaskAssigned = 1,
        TaskStatusChanged = 2,
        TaskDueSoon = 3,
        TaskOverdue = 4,

        // ── Reports ───────────────────────────────────────────
        ReportReady = 5,
        ReportFailed = 6,

        // ── Risk & Intelligence ───────────────────────────────
        AnomalyDetected = 7,
        HighRiskTask = 8,

        // ── Identity & Roles ──────────────────────────────────
        RoleChanged = 9,

        // ── Password Reset Workflow ───────────────────────────
        PasswordResetRequested = 10,
        PasswordResetApproved = 11,
        PasswordResetDenied = 12,
    }
}
