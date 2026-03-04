using ERP.BLL.DTOs;

namespace ERP.PL.ViewModels.Home
{
    public class ManagerHomeDashboardViewModel
    {
        // ── User Context ──
        public string UserDisplayName { get; set; } = string.Empty;
        public string UserRole { get; set; } = "DepartmentManager";
        public bool IsDepartmentManager => UserRole == "DepartmentManager";
        public bool IsProjectManager => UserRole == "ProjectManager";

        // ── Team Overview ──
        public int TeamMemberCount { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public List<TeamMemberSummary> TeamMembers { get; set; } = new();

        // ── Workload Overview ──
        public int TotalActiveTasks { get; set; }
        public int OverdueTaskCount { get; set; }
        public int HighPriorityTaskCount { get; set; }
        public int BlockedTaskCount { get; set; }

        // ── Overdue Tasks ──
        public List<ManagerTaskItem> OverdueTasks { get; set; } = new();

        // ── High Risk Tasks ──
        public List<ManagerTaskItem> HighRiskTasks { get; set; } = new();

        // ── Project Progress ──
        public List<ManagerProjectProgress> Projects { get; set; } = new();

        // ── Department Performance ──
        public int TasksCompletedThisMonth { get; set; }
        public int TasksCreatedThisMonth { get; set; }
        public double CompletionRate { get; set; }
    }

    public class TeamMemberSummary
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int ActiveTasks { get; set; }
        public int OverdueTasks { get; set; }
        public string WorkloadLabel { get; set; } = "Normal";
        public string WorkloadBadgeClass { get; set; } = "bg-success";
    }

    public class ManagerTaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string AssigneeName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";
        public string PriorityBadgeClass { get; set; } = "bg-secondary";
        public string Status { get; set; } = "New";
        public DateTime? DueDate { get; set; }
        public int? RiskScore { get; set; }
        public string RiskLevel { get; set; } = "Low";
    }

    public class ManagerProjectProgress
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusBadgeClass { get; set; } = "bg-secondary";
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int ProgressPercent { get; set; }
    }
}
