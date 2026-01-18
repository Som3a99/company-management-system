using ERP.DAL.Models;
using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.Employee
{
    public class EmployeeViewModel
    {
        #region Properties

        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [MinLength(3)]
        public string FirstName { get; set; } = null!;
        [Required]
        [MaxLength(50)]
        [MinLength(3)]
        public string LastName { get; set; } = null!;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
        [Required]
        [Display( Name = "Phone Number")]
        [Phone]
        public string PhoneNumber { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string Position { get; set; } = null!;

        [Required]
        [DataType(DataType.Date)]
        [Display( Name = "Hire Date")]
        public DateTime HireDate { get; set; }
        [Required]
        [DataType(DataType.Currency)]
        public decimal Salary { get; set; }
        [Display( Name = "Is Active")]
        public bool IsActive { get; set; }
        [Display( Name = "Is Deleted")]
        public bool IsDeleted { get; set; }
        [Display( Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        public IFormFile? Image { get; set; }

        public string? ImageUrl { get; set; }

        [Required]
        public Gender Gender { get; set; }

        [Required]
        public int DepartmentId { get; set; }

        public DAL.Models.Department? Department { get; set; }
        #endregion
    }
}
