namespace ERP.BLL.DTOs
{
    public class TaskAssignmentSuggestion
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int CompositeScore { get; set; }
        public int ExperienceScore { get; set; }
        public int AccuracyScore { get; set; }
        public int AvailabilityScore { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }
}
