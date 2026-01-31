using ERP.DAL.Models;

namespace ERP.PL.ViewModels.Employee
{
    public class EmployeeProfileViewModel
    {
        // Basic Information
        public int Id { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string FullName => $"{FirstName} {LastName}";
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Position { get; set; } = null!;
        public Gender Gender { get; set; }
        public string ImageUrl { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime HireDate { get; set; }
        public decimal Salary { get; set; }
        public DateTime CreatedAt { get; set; }

        // Department Information
        public int DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string? DepartmentCode { get; set; }

        // Managed Department (if employee is a manager)
        public int? ManagedDepartmentId { get; set; }
        public string? ManagedDepartmentName { get; set; }
        public string? ManagedDepartmentCode { get; set; }
        public int ManagedDepartmentEmployeeCount { get; set; }

        // Managed Project (if employee is a project manager)
        public int? ManagedProjectId { get; set; }
        public string? ManagedProjectName { get; set; }
        public string? ManagedProjectCode { get; set; }
        public int? ManagedProjectDepartmentId { get; set; }
        public string? ManagedProjectDepartmentName { get; set; }

        // Assigned Project (project employee is working on)
        public int? AssignedProjectId { get; set; }
        public string? AssignedProjectName { get; set; }
        public string? AssignedProjectCode { get; set; }
        public int? AssignedProjectManagerId { get; set; }
        public string? AssignedProjectManagerName { get; set; }
        public int? AssignedProjectDepartmentId { get; set; }
        public string? AssignedProjectDepartmentName { get; set; }
    }
}
