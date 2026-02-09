using ERP.DAL.Models;

namespace ERP.PL.ViewModels.Account
{
    /// <summary>
    /// ViewModel for displaying password reset requests in IT Admin portal
    /// </summary>
    public class PasswordResetRequestViewModel
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = null!;
        public string UserEmail { get; set; } = null!;
        public string? EmployeeName { get; set; }
        public ResetStatus Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public string? DenialReason { get; set; }
        public string? IpAddress { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpiresAt && Status == ResetStatus.Pending;
        public TimeSpan TimeUntilExpiry => ExpiresAt - DateTime.UtcNow;
    }
}
