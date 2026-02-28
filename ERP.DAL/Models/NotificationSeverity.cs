namespace ERP.DAL.Models
{
    public enum NotificationSeverity
    {
        Info = 0,      // Default — blue icon, normal priority
        Warning = 1,   // Amber icon — task overdue, due soon, high-risk
        Critical = 2   // Red icon + toast — anomaly, password reset urgent
    }
}
