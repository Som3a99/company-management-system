namespace ERP.PL.ViewModels.Home
{
    public class ITAdminHomeDashboardViewModel
    {
        // ── User Context ──
        public string UserDisplayName { get; set; } = string.Empty;

        // ── System Health ──
        public string ApplicationStatus { get; set; } = "Operational";
        public string ApplicationStatusClass { get; set; } = "status-green";
        public string DatabaseStatus { get; set; } = "Connected";
        public string DatabaseStatusClass { get; set; } = "status-green";
        public int ActiveSessionCount { get; set; }
        public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;

        // ── Security Monitoring ──
        public int FailedLoginAttempts24h { get; set; }
        public int LockedAccountCount { get; set; }
        public int SuspiciousActivities48h { get; set; }
        public int RoleChanges7d { get; set; }
        public int PasswordResets7d { get; set; }
        public List<SecurityEventItem> RecentSecurityEvents { get; set; } = new();

        // ── Infrastructure Metrics ──
        public int PendingReportJobs { get; set; }
        public int ProcessingReportJobs { get; set; }
        public int FailedReportJobs { get; set; }
        public int CompletedReportJobs24h { get; set; }
        public double CacheHitRatio { get; set; }
        public long CacheEntryCount { get; set; }
        public long CacheEstimatedSize { get; set; }
        public string? CachePressureWarning { get; set; }

        // ── Identity & Access Overview ──
        public int TotalUserAccounts { get; set; }
        public int ActiveUserAccounts { get; set; }
        public int InactiveUserAccounts { get; set; }
        public int UsersRequiringPasswordChange { get; set; }
        public int AccountsCreatedThisWeek { get; set; }
        public List<RoleDistributionItem> RoleDistribution { get; set; } = new();

        // ── Audit & Logs ──
        public int TotalAuditEvents24h { get; set; }
        public int ErrorEvents24h { get; set; }
        public int WarningAnomalies { get; set; }
        public int CriticalAnomalies { get; set; }
        public List<AuditEventItem> RecentAuditEvents { get; set; } = new();

        // ── Password Reset Requests ──
        public int PendingResetRequests { get; set; }
        public int ExpiredResetRequests { get; set; }

        // ── Quick Stats ──
        public int TotalDepartments { get; set; }
        public int TotalEmployees { get; set; }
    }

    public class SecurityEventItem
    {
        public string Icon { get; set; } = "fas fa-shield-alt";
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public string SeverityClass { get; set; } = "status-blue";
        public DateTime Timestamp { get; set; }
    }

    public class RoleDistributionItem
    {
        public string RoleName { get; set; } = string.Empty;
        public int Count { get; set; }
        public string BadgeClass { get; set; } = "bg-secondary";
    }

    public class AuditEventItem
    {
        public string Action { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string? ResourceType { get; set; }
        public bool Succeeded { get; set; }
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
    }
}
