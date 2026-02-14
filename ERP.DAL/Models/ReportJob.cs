namespace ERP.DAL.Models
{
    public enum ReportJobStatus
    {
        Pending = 1,
        Processing = 2,
        Completed = 3,
        Failed = 4
    }

    public enum ReportType
    {
        Tasks = 1,
        Projects = 2,
        Departments = 3,
        Audit = 4
    }

    public enum ReportFileFormat
    {
        Csv = 1,
        Pdf = 2,
        Excel = 3
    }

    public class ReportJob : Base
    {
        public string RequestedByUserId { get; set; } = null!;
        public ReportType ReportType { get; set; }
        public ReportFileFormat Format { get; set; }
        public ReportJobStatus Status { get; set; } = ReportJobStatus.Pending;

        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        public string? FiltersJson { get; set; }
        public string? OutputPath { get; set; }
        public string? FailureReason { get; set; }
    }
}
