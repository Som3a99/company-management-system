namespace ERP.DAL.Models
{
    public class ProjectEmployee
    {
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime AssignedAt { get; set; }
        public string AssignedBy { get; set; } = null!;
    }
}
