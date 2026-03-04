using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.Account
{
    /// <summary>
    /// ViewModel for forgot password request form.
    /// Supports two paths: email-based self-service reset and IT Admin ticket flow.
    /// </summary>
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Company Email Address")]
        public string Email { get; set; } = null!;

        /// <summary>
        /// True if the user indicates they have access to their email inbox.
        /// Determines whether to send a self-service reset link or create an IT ticket.
        /// </summary>
        public bool HasEmailAccess { get; set; }
    }
}
