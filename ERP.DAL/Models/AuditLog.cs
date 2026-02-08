namespace ERP.DAL.Models
{
    /// <summary>
    /// Audit trail for all security-relevant actions
    /// WHO did WHAT to WHICH resource WHEN and WHETHER it succeeded
    /// </summary>
    public class AuditLog : Base
    {
        /// <summary>
        /// ApplicationUser.Id who performed the action
        /// </summary>
        public string UserId { get; set; } = null!;

        /// <summary>
        /// User's email at time of action
        /// (in case user deleted later)
        /// </summary>
        public string UserEmail { get; set; } = null!;

        /// <summary>
        /// Action performed (e.g., "LOGIN", "EDIT_EMPLOYEE", "DELETE_DEPARTMENT")
        /// </summary>
        public string Action { get; set; } = null!;

        /// <summary>
        /// Type of resource (e.g., "Employee", "Department", "Project")
        /// </summary>
        public string? ResourceType { get; set; }

        /// <summary>
        /// ID of resource affected (e.g., EmployeeId, DepartmentId)
        /// </summary>
        public int? ResourceId { get; set; }

        /// <summary>
        /// Additional details (JSON format for flexibility)
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Did the action succeed?
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// When did this happen?
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// IP address of user
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent (browser info)
        /// </summary>
        public string? UserAgent { get; set; }
    }
}
