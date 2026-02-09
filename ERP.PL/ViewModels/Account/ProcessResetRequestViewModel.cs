using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.Account
{
    /// <summary>
    /// ViewModel for approving/denying password reset requests
    /// </summary>
    public class ProcessResetRequestViewModel
    {
        public int RequestId { get; set; }
        public string TicketNumber { get; set; } = null!;
        public string UserEmail { get; set; } = null!;

        [Display(Name = "Temporary Password")]
        [DataType(DataType.Password)]
        public string? TemporaryPassword { get; set; }

        [Display(Name = "Reason for Denial")]
        public string? DenialReason { get; set; }
    }
}
