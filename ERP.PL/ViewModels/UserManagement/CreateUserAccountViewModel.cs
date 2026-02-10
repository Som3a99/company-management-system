using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.UserManagement
{
    /// <summary>
    /// ViewModel for creating user accounts for existing employees
    /// </summary>
    public class CreateUserAccountViewModel
    {
        public int EmployeeId { get; set; }

        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; } = null!;

        [Display(Name = "Employee Email")]
        public string EmployeeEmail { get; set; } = null!;

        [Display(Name = "Department")]
        public string? DepartmentName { get; set; }

        [Display(Name = "Position")]
        public string Position { get; set; } = null!;

        /// <summary>
        /// System-generated password (displayed once to admin after creation)
        /// </summary>
        public string? GeneratedPassword { get; set; }

        /// <summary>
        /// Indicates if employee is a department manager
        /// </summary>
        public bool IsDepartmentManager { get; set; }

        /// <summary>
        /// Indicates if employee is a project manager
        /// </summary>
        public bool IsProjectManager { get; set; }

        /// <summary>
        /// Selected roles to assign to the user account (e.g., "Employee", "DepartmentManager", "ProjectManager")
        /// </summary>
        public List<string> SelectedRoles { get; set; } = new List<string>();
        public List<SelectListItem> AvailableRoles { get; set; } = new List<SelectListItem>();
        
        /// <summary>
        /// Roles assigned to the created account (for confirmation view)
        /// </summary>
        public List<string> AssignedRoles { get; set; } = new List<string>();
    }
}
