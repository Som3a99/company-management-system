namespace ERP.PL.Services
{
    public interface IAuditService
    {
        Task LogAsync(
            string userId,
            string userEmail,
            string action,
            string? resourceType = null,
            int? resourceId = null,
            bool succeeded = true,
            string? errorMessage = null,
            string? details = null);
    }
}
