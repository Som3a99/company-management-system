namespace ERP.BLL.DTOs
{
    public class ReportSummaryInput
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int BlockedTasks { get; set; }
        public int ActiveProjects { get; set; }
        public string DepartmentWithMostTasks { get; set; } = string.Empty;
    }
}
