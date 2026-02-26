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

        // Phase 3 — Proactive Intelligence Widgets
        public DashboardIntelligenceData? Intelligence { get; set; }
    }
}
