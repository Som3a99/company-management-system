using ERP.DAL.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.Employee
{
    public class EmployeeViewModel
    {
        #region Properties

        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [MaxLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        [MinLength(3, ErrorMessage = "First name must be at least 3 characters")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "First name can only contain letters and spaces")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = null!;

        [Required(ErrorMessage = "Last name is required")]
        [MaxLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        [MinLength(3, ErrorMessage = "Last name must be at least 3 characters")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Last name can only contain letters and spaces")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = null!;

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [MaxLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        [Remote(action: "IsEmailUnique", controller: "Employee",
            AdditionalFields = nameof(Id),
            ErrorMessage = "This email address is already registered")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        [RegularExpression(@"^[\+]?[(]?[0-9]{1,4}[)]?[-\s\.]?[(]?[0-9]{1,4}[)]?[-\s\.]?[0-9]{1,9}$",
            ErrorMessage = "Please enter a valid international phone number (e.g., +1 (555) 123-4567, 0123456789)")]
        public string PhoneNumber { get; set; } = null!;

        [Required(ErrorMessage = "Position is required")]
        [MaxLength(100, ErrorMessage = "Position cannot exceed 100 characters")]
        [MinLength(3, ErrorMessage = "Position must be at least 3 characters")]
        public string Position { get; set; } = null!;

        [Required(ErrorMessage = "Hire date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Hire Date")]
        public DateTime HireDate { get; set; }

        [Required(ErrorMessage = "Salary is required")]
        [DataType(DataType.Currency)]
        [Display(Name = "Salary")]
        public decimal Salary { get; set; }
        [Display( Name = "Is Active")]
        public bool IsActive { get; set; }
        [Display( Name = "Is Deleted")]
        public bool IsDeleted { get; set; }
        [Display( Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        public IFormFile? Image { get; set; }

        public string? ImageUrl { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        public Gender Gender { get; set; }

        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }

        public byte[]? RowVersion { get; set; }

        public DAL.Models.Department? Department { get; set; }

        // Project Assignment
        [Display(Name = "Assigned Project")]
        public int? ProjectId { get; set; }

        public DAL.Models.Project? Project { get; set; }
        #endregion
    }
}
