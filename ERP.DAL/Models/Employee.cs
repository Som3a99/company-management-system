using System.ComponentModel.DataAnnotations;

namespace ERP.DAL.Models
{
    public enum Gender
    {
        Male = 1,
        Female = 2,
    }
    public class Employee : Base
    {
        #region Properties
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Position { get; set; } = null!;
        public DateTime HireDate { get; set; }
        public decimal Salary { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ImageUrl { get; set; } = null!;
        public Gender Gender { get; set; }
        #endregion

        #region Navigational Property
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // Managed Department Inverse Property
        public Department? ManagedDepartment { get; set; }

        // Managed Project Inverse Property (One-to-One)
        public Project? ManagedProject { get; set; }

        // Project Assignment (legacy: each employee works on one project)
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        // Project assignments through junction table
        public ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new HashSet<ProjectEmployee>();

        // Tasks assigned to employee
        public ICollection<TaskItem> AssignedTasks { get; set; } = new HashSet<TaskItem>();

        /// <summary>
        /// Foreign key to Identity user account
        /// Null if employee doesn't have login access yet
        /// </summary>
        public string? ApplicationUserId { get; set; }

        /// <summary>
        /// Navigation property to Identity account
        /// </summary>
        public ApplicationUser? ApplicationUser { get; set; }
        #endregion
    }
}
