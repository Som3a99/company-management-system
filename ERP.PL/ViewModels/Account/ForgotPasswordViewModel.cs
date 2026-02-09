using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.Account
{
    /// <summary>
    /// ViewModel for forgot password request form
    /// </summary>
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Company Email Address")]
        public string Email { get; set; } = null!;
    }
}
