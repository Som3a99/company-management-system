using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.Account
{
    /// <summary>
    /// ViewModel for self-service password reset (token-based, via email link).
    /// </summary>
    public class ResetPasswordViewModel
    {
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public string Token { get; set; } = null!;

        [Required(ErrorMessage = "New password is required")]
        [StringLength(128, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = null!;

        [Required(ErrorMessage = "Please confirm your new password")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
