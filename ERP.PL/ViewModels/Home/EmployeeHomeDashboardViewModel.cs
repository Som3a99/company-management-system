using ERP.DAL.Models;

namespace ERP.PL.ViewModels.Home
{
    public class EmployeeHomeDashboardViewModel
    {
        // ── Personal Info ──
        public string UserDisplayName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int EmployeeId { get; set; }

        // ── My Department ──
        public string DepartmentName { get; set; } = "Unassigned";
        public string ManagerName { get; set; } = "N/A";

        // ── My Projects ──
        public List<EmployeeProjectSummary> MyProjects { get; set; } = new();

        // ── My Tasks ──
        public List<EmployeeTaskItem> AssignedTasks { get; set; } = new();
        public List<EmployeeTaskItem> DueSoonTasks { get; set; } = new();
        public List<EmployeeTaskItem> OverdueTasks { get; set; } = new();

        // ── Personal Task Summary ──
        public int TotalAssignedTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int OverdueCount { get; set; }

        // ── Recent Notifications ──
        public List<NotificationItem> RecentNotifications { get; set; } = new();
        public int UnreadNotificationCount { get; set; }
    }

    public class EmployeeProjectSummary
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusBadgeClass { get; set; } = "bg-secondary";
        public int TaskCount { get; set; }
    }

    public class EmployeeTaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";
        public string PriorityBadgeClass { get; set; } = "bg-secondary";
        public string Status { get; set; } = "New";
        public string StatusBadgeClass { get; set; } = "bg-secondary";
        public DateTime? DueDate { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsDueSoon { get; set; }
    }

    public class NotificationItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
        public string? LinkUrl { get; set; }
        public bool IsRead { get; set; }
        public string TimeAgo { get; set; } = string.Empty;
    }
}
