namespace ERP.BLL.DTOs
{
    public class DashboardIntelligenceData
    {
        public int HighRiskTaskCount { get; set; }
        public int OverloadedEmployeeCount { get; set; }
        public int BehindScheduleProjectCount { get; set; }
        public TeamHealthScoreResult TeamHealth { get; set; } = new();
        public int AnomalyCount { get; set; }
    }
}
