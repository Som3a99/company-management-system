namespace ERP.BLL.DTOs
{
    public class EmployeeWorkloadResult
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int ActiveTasks { get; set; }
        public int RemainingHours { get; set; }
        public int LoadScore { get; set; }
        public string Label { get; set; } = "Available";
    }
}
