namespace ERP.PL.ViewModels.Account
{
    /// <summary>
    /// ViewModel for password reset request confirmation.
    /// Supports both email-based and IT ticket-based flows.
    /// </summary>
    public class ForgotPasswordConfirmationViewModel
    {
        public string Message { get; set; } = null!;
        public string? TicketNumber { get; set; }

        /// <summary>
        /// True when the confirmation is for an email-based self-service reset.
        /// </summary>
        public bool IsEmailReset { get; set; }
    }
}
