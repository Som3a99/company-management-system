namespace ERP.DAL.Models
{
    public enum ProjectStatus
    {
        None = 0,
        Planning = 1,
        InProgress = 2,
        OnHold = 3,
        Completed = 4,
        Cancelled = 5
    }

    public class Project : Base
    {
        #region Properties
        public string ProjectCode { get; set; } = null!;
        public string ProjectName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal Budget { get; set; }
        public ProjectStatus Status { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        #endregion

        #region Navigation Properties
        // Department relationship
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        // Project Manager relationship (One-to-One)
        public int? ProjectManagerId { get; set; }
        public Employee? ProjectManager { get; set; }

        // Project Employees relationship (One-to-Many)
        public ICollection<Employee> Employees { get; set; } = new HashSet<Employee>();
        #endregion
    }
}
