using ERP.DAL.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
namespace ERP.PL.ViewModels.Department
{
    public class DepartmentViewModel
    {
        public int Id { get; set; }

        // "CK_Department_DepartmentCode_Format", "DepartmentCode LIKE '[A-Z][A-Z][A-Z]_[0-9][0-9][0-9]'"

        [Required(ErrorMessage = "Department code is required")]
        [RegularExpression(@"^[A-Z]{3}_[0-9]{3}$",
            ErrorMessage = "Department code must follow format ABC_123 (e.g., ENG_101, HR_001)")]
        [Remote(action: "IsDepartmentCodeUnique", controller: "Department",
            AdditionalFields = nameof(Id),
            ErrorMessage = "This department code is already in use")]
        [Display(Name = "Department Code")]
        public string DepartmentCode { get; set; } = null!;

        [Required(ErrorMessage = "Department name is required")]
        [MaxLength(100, ErrorMessage = "Department name cannot exceed 100 characters")]
        [MinLength(3, ErrorMessage = "Department name must be at least 3 characters")]
        [Display(Name = "Department Name")]
        public string DepartmentName { get; set; } = null!;

        [Required(ErrorMessage = "Created date is required")]
        [Display(Name = "Created At")]
        [DataType(DataType.Date)]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Is Deleted")]
        public bool IsDeleted { get; set; }

        public ICollection<DAL.Models.Employee> Employees { get; set; } = new HashSet<DAL.Models.Employee>();

        [Display(Name = "Department Manager")]
        public int? ManagerId { get; set; }

        public DAL.Models.Employee? Manager { get; set; }


    }
}
