namespace ERP.PL.ViewModels.UserManagement
{
    /// <summary>
    /// ViewModel for listing all user accounts
    /// </summary>
    public class UserAccountListViewModel
    {
        public string UserId { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public bool IsActive { get; set; }
        public bool RequirePasswordChange { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
        public int AccessFailedCount { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
    }
}
