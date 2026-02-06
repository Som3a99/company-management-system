using ERP.DAL.Models;

namespace ERP.PL.ViewModels.Project
{
    public class ProjectProfileViewModel
    {
        // Basic Information
        public int Id { get; set; }
        public string ProjectCode { get; set; } = null!;
        public string ProjectName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal Budget { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        // Department Information
        public int DepartmentId { get; set; }
        public string? DepartmentCode { get; set; }
        public string? DepartmentName { get; set; }

        // Project Manager Information
        public int? ProjectManagerId { get; set; }
        public string? ManagerFirstName { get; set; }
        public string? ManagerLastName { get; set; }
        public string? ManagerFullName => ManagerFirstName != null && ManagerLastName != null
            ? $"{ManagerFirstName} {ManagerLastName}"
            : null;
        public string? ManagerPosition { get; set; }
        public string? ManagerEmail { get; set; }
        public string? ManagerImageUrl { get; set; }

        // Statistics
        public int TotalTeamMembers { get; set; }
        public int ActiveTeamMembers { get; set; }
        public int InactiveTeamMembers { get; set; }
        public decimal AverageTeamSalary { get; set; }
        public decimal TotalTeamSalaryExpense { get; set; }
        public int DaysInProgress { get; set; }
        public int? DaysRemaining { get; set; }
        public decimal BudgetPerTeamMember { get; set; }

        // Team Members (for display - recent/top employees)
        public List<TeamMemberSummary> TeamMembers { get; set; } = new();

        // Nested class for team member summary
        public class TeamMemberSummary
        {
            public int Id { get; set; }
            public string FirstName { get; set; } = null!;
            public string LastName { get; set; } = null!;
            public string FullName => $"{FirstName} {LastName}";
            public string Position { get; set; } = null!;
            public string Email { get; set; } = null!;
            public string ImageUrl { get; set; } = null!;
            public bool IsActive { get; set; }
            public int? DepartmentId { get; set; }
            public string? DepartmentName { get; set; }
        }
    }
}
