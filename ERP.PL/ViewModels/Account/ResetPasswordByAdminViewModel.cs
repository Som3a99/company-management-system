using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.Account
{
    /// <summary>
    /// ViewModel for IT Admin manual password reset
    /// Used when employee doesn't have email access
    /// </summary>
    public class ResetPasswordByAdminViewModel
    {
        [Required(ErrorMessage = "Employee email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Employee Email")]
        public string EmployeeEmail { get; set; } = null!;

        [Required(ErrorMessage = "Temporary password is required")]
        [StringLength(100, MinimumLength = 12,
            ErrorMessage = "Password must be at least 12 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "Temporary Password")]
        public string TemporaryPassword { get; set; } = null!;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("TemporaryPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = null!;

        /// <summary>
        /// Notes about identity verification
        /// (e.g., "Verified via employee ID card")
        /// </summary>
        [Display(Name = "Verification Notes")]
        [MaxLength(500)]
        public string? VerificationNotes { get; set; }
    }
}
