namespace ERP.BLL.DTOs
{
    public class EmployeeEstimateAccuracy
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int CompletedTasks { get; set; }
        public double Ratio { get; set; }
        public string Label { get; set; } = "Accurate";
    }
}
