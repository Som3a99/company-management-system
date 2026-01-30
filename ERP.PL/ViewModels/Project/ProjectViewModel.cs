using ERP.DAL.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.Project
{
    public class ProjectViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Project code is required")]
        [RegularExpression(@"^PRJ-[0-9]{4}-[0-9]{3}$",
            ErrorMessage = "Project code must follow format PRJ-YYYY-XXX (e.g., PRJ-2026-001)")]
        [Remote(action: "IsProjectCodeUnique", controller: "Project",
            AdditionalFields = nameof(Id),
            ErrorMessage = "This project code is already in use")]
        [Display(Name = "Project Code")]
        public string ProjectCode { get; set; } = null!;

        [Required(ErrorMessage = "Project name is required")]
        [MaxLength(200, ErrorMessage = "Project name cannot exceed 200 characters")]
        [MinLength(3, ErrorMessage = "Project name must be at least 3 characters")]
        [Display(Name = "Project Name")]
        public string ProjectName { get; set; } = null!;

        [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [Required(ErrorMessage = "Budget is required")]
        [Range(0, 999999999.99, ErrorMessage = "Budget must be between 0 and 999,999,999.99")]
        [DataType(DataType.Currency)]
        [Display(Name = "Budget")]
        public decimal Budget { get; set; }

        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Status")]
        public ProjectStatus Status { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Display(Name = "Project Manager")]
        public int? ProjectManagerId { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        public bool IsDeleted { get; set; }

        // Navigation properties for display
        public DAL.Models.Department? Department { get; set; }
        public DAL.Models.Employee? ProjectManager { get; set; }

        // Assigned Employees
        [Display(Name = "Assigned Employees")]
        public ICollection<DAL.Models.Employee>? AssignedEmployees { get; set; }
    }
}
