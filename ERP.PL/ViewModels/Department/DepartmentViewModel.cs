using System.ComponentModel.DataAnnotations;
using ERP.DAL.Models;
namespace ERP.PL.ViewModels.Department
{
    public class DepartmentViewModel
    {
        public int Id { get; set; }

        // "CK_Department_DepartmentCode_Format", "DepartmentCode LIKE '[A-Z][A-Z][A-Z]_[0-9][0-9][0-9]'"

        [Required]
        [RegularExpression(@"^[A-Z]{3}_[0-9]{3}$", ErrorMessage = "Department code must follow format ABC_123 (e.g. ENG_101)")]
        public string DepartmentCode { get; set; } = null!;
        [Required]
        public string DepartmentName { get; set; } = null!;
        [Required]
        [Display(Name = "Created At")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; }

        public ICollection<DAL.Models.Employee> Employees { get; set; } = new HashSet<DAL.Models.Employee>();

    }
}
