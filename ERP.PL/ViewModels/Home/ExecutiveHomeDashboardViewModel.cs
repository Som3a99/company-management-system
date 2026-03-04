using ERP.BLL.DTOs;

namespace ERP.PL.ViewModels.Home
{
    public class ExecutiveHomeDashboardViewModel
    {
        // ── User Context ──
        public string UserDisplayName { get; set; } = string.Empty;
        public string UserRole { get; set; } = "CEO";

        // ── KPI Overview ──
        public int ActiveDepartments { get; set; }
        public int TotalEmployees { get; set; }
        public int ActiveProjects { get; set; }
        public int SystemHealthPercentage { get; set; }

        // ── Task KPIs ──
        public int TotalActiveTasks { get; set; }
        public int CompletedTasksThisMonth { get; set; }
        public int OverdueTaskCount { get; set; }

        // ── Intelligence Data ──
        public DashboardIntelligenceData? Intelligence { get; set; }

        // ── Anomaly Alerts ──
        public List<AnomalyAlertItem> AnomalyAlerts { get; set; } = new();

        // ── High Risk Tasks ──
        public List<ExecutiveTaskItem> HighRiskTasks { get; set; } = new();

        // ── Overdue Trends ──
        public int OverdueTasksTrend { get; set; } // positive = increasing, negative = decreasing
        public int OverdueLastWeek { get; set; }
        public int OverdueThisWeek { get; set; }

        // ── Team Health Score ──
        public TeamHealthScoreResult? TeamHealth { get; set; }

        // ── Executive Digest Insights ──
        public List<DigestInsight> DigestInsights { get; set; } = new();

        // ── Department Performance Summary ──
        public List<DepartmentKpi> DepartmentPerformance { get; set; } = new();
    }

    public class AnomalyAlertItem
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public string SeverityBadgeClass { get; set; } = "bg-info";
        public DateTime DetectedAt { get; set; }
    }

    public class ExecutiveTaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string AssigneeName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public int RiskScore { get; set; }
        public string RiskLevel { get; set; } = "Low";
        public DateTime? DueDate { get; set; }
    }

    public class DigestInsight
    {
        public string Icon { get; set; } = "fas fa-info-circle";
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
    }

    public class DepartmentKpi
    {
        public int Id { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public int ActiveTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double CompletionRate { get; set; }
    }
}
