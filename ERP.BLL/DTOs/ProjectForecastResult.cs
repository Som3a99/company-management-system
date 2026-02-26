namespace ERP.BLL.DTOs
{
    public class ProjectForecastResult
    {
        public DateTime EstimatedCompletionDate { get; set; }
        public int DaysBehindSchedule { get; set; }
        public string Status { get; set; } = "On Track";
        public double Velocity { get; set; }
        public int RemainingTasks { get; set; }
        public int CompletedTasksLast30Days { get; set; }
    }
}
