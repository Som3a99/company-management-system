using ERP.BLL.DTOs;

namespace ERP.PL.ViewModels.Home
{
    public class HomeDashboardViewModel
    {
        public int ActiveDepartments { get; set; }
        public int TotalEmployees { get; set; }
        public int ActiveProjects { get; set; }
        public int SystemHealthPercentage { get; set; }
        public int VisibleTasks { get; set; }
        public int MyOpenTasks { get; set; }

        // Role context
        public string UserRole { get; set; } = "Employee";
        public string UserDisplayName { get; set; } = "";

        // Role helpers
        public bool IsExecutive => UserRole is "CEO" or "ITAdmin";
        public bool IsManager => UserRole is "DepartmentManager" or "ProjectManager";
        public bool IsEmployee => UserRole == "Employee";
        public bool CanSeeOrgStats => IsExecutive || IsManager;

        // Phase 3 — Proactive Intelligence Widgets
        public DashboardIntelligenceData? Intelligence { get; set; }
    }
}
