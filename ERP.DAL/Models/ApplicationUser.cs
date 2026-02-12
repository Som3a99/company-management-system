using Microsoft.AspNetCore.Identity;

namespace ERP.DAL.Models
{
    /// <summary>
    /// Represents an application user account (authentication)
    /// Links to Employee entity for business context
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// Optional link to Employee entity
        /// Null for pure IT accounts (CEO admin, IT Admin)
        /// </summary>
        public int? EmployeeId { get; set; }

        /// <summary>
        /// Navigation property to Employee
        /// Used to populate claims (DepartmentId, etc.)
        /// </summary>
        public Employee? Employee { get; set; }

        /// <summary>
        /// Account status (separate from Employee.IsActive)
        /// Can disable login without affecting employee record
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Audit trail: when was account created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Force password change on next login
        /// Used for IT Admin password resets
        /// </summary>
        public bool RequirePasswordChange { get; set; } = false;

        public ICollection<TaskItem> CreatedTasks { get; set; } = new HashSet<TaskItem>();
        public ICollection<TaskComment> TaskComments { get; set; } = new HashSet<TaskComment>();
    }
}
