using ERP.DAL.Models;

namespace ERP.PL.ViewModels.Department
{
    public class DepartmentProfileViewModel
    {
        // Basic Information
        public int Id { get; set; }
        public string DepartmentCode { get; set; } = null!;
        public string DepartmentName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        // Manager Information
        public int? ManagerId { get; set; }
        public string? ManagerName { get; set; }
        public string? ManagerPosition { get; set; }
        public string? ManagerEmail { get; set; }
        public string? ManagerImageUrl { get; set; }

        // Statistics
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int InactiveEmployees { get; set; }
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public decimal AverageSalary { get; set; }
        public decimal TotalBudget { get; set; }

        // Collections (for display in cards/lists)
        public ICollection<EmployeeSummary> RecentEmployees { get; set; } = new List<EmployeeSummary>();
        public ICollection<ProjectSummary> RecentProjects { get; set; } = new List<ProjectSummary>();

        // Nested classes for summaries
        public class EmployeeSummary
        {
            public int Id { get; set; }
            public string Name { get; set; } = null!;
            public string Position { get; set; } = null!;
            public string ImageUrl { get; set; } = null!;
            public bool IsActive { get; set; }
            public int? ProjectId { get; set; }
            public string? ProjectName { get; set; }
        }

        public class ProjectSummary
        {
            public int Id { get; set; }
            public string ProjectCode { get; set; } = null!;
            public string ProjectName { get; set; } = null!;
            public ProjectStatus Status { get; set; }
            public int? ManagerId { get; set; }
            public string? ManagerName { get; set; }
            public int TeamSize { get; set; }
            public decimal Budget { get; set; }
        }
    }
}
