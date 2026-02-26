namespace ERP.BLL.DTOs
{
    public class WeeklyDigestData
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Task Summary
        public int TotalActiveTasks { get; set; }
        public int TasksCompletedThisWeek { get; set; }
        public int TasksCreatedThisWeek { get; set; }
        public int HighRiskTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }

        // Workforce
        public int OverloadedEmployeeCount { get; set; }
        public List<string> OverloadedEmployeeNames { get; set; } = new();

        // Projects
        public int BehindScheduleProjectCount { get; set; }
        public List<string> BehindScheduleProjectNames { get; set; } = new();

        // Security
        public int AnomaliesDetected { get; set; }

        // Team Health
        public TeamHealthScoreResult TeamHealth { get; set; } = new();
    }
}
