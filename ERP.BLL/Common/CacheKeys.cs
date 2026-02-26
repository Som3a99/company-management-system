namespace ERP.BLL.Common
{
    public static class CacheKeys
    {
        public const string DepartmentsAll = "erp:dept:all";
        public const string ProjectsAll = "erp:proj:all";
        public const string AvailableProjectManagersAll = "erp:emp:project-managers:all";
        public const string ItAdminDashboard = "erp:dashboard:itadmin";
        public const string UserClaimsPrefix = "erp:user:";

        public const string ReportTasksPrefix = "erp:report:tasks:";
        public const string ReportProjectsPrefix = "erp:report:projects:";
        public const string ReportDepartmentsPrefix = "erp:report:departments:";
        public const string ReportAuditPrefix = "erp:report:audit:";
        public const string ReportAccuracyPrefix = "erp:report:accuracy:";
        public const string AiSummaryPrefix = "erp:ai:summary:";
        public const string ProjectForecastPrefix = "erp:forecast:project:";

        // Phase 3 — Proactive Intelligence
        public const string SuggestionPrefix = "erp:suggest:";
        public const string AnomalyPrefix = "erp:anomaly:";
        public const string TeamHealth = "erp:team-health";
        public const string DashboardIntelligence = "erp:dashboard:intelligence";
        public const string DigestWeekly = "erp:digest:weekly";
    }
}
