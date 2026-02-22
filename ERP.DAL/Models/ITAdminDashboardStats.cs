namespace ERP.DAL.Models
{
    public sealed class ITAdminDashboardStats
    {
        public int PendingResets { get; init; }
        public int ExpiredResets { get; init; }
        public int LockedAccounts { get; init; }
    }
}
