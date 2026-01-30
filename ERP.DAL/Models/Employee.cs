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
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        // Managed Department Inverse Property
        public Department? ManagedDepartment { get; set; }

        // Managed Project Inverse Property (One-to-One)
        public Project? ManagedProject { get; set; }

        // Project Assignment (Each employee works on one project)
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }
        #endregion
    }
}
