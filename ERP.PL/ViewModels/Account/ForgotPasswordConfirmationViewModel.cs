namespace ERP.PL.ViewModels.Account
{
    /// <summary>
    /// ViewModel for password reset request confirmation
    /// </summary>
    public class ForgotPasswordConfirmationViewModel
    {
        public string Message { get; set; } = null!;
        public string? TicketNumber { get; set; }
    }
}
