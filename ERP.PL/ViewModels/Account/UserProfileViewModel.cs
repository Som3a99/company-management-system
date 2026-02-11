using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.Account
{
    public class UserProfileViewModel
    {
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public DateTime AccountCreatedAt { get; set; }
        public string AccountStatus { get; set; } = "Active";
        public string PrimaryRole { get; set; } = "Employee";
        public List<string> Roles { get; set; } = new();

        public int? EmployeeId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Position { get; set; }
        public string? DepartmentName { get; set; }
        public string? DepartmentCode { get; set; }
        public DateTime? HireDate { get; set; }
        public string ImageUrl { get; set; } = "/uploads/images/avatar-user.png";

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfileImage { get; set; }
    }
}
