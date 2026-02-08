using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;

namespace ERP.PL.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(
            string userId,
            string userEmail,
            string action,
            string? resourceType = null,
            int? resourceId = null,
            bool succeeded = true,
            string? errorMessage = null,
            string? details = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;

                var auditLog = new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = action,
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    Succeeded = succeeded,
                    ErrorMessage = errorMessage,
                    Details = details,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = httpContext?.Request.Headers["User-Agent"].ToString()
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    $"Audit: {userEmail} - {action} - {resourceType}:{resourceId} - {(succeeded ? "SUCCESS" : "FAILED")}");
            }
            catch (Exception ex)
            {
                // CRITICAL: Audit logging failure should NOT break the app
                _logger.LogError(ex, "Failed to write audit log");
            }
        }
    }
}
