namespace ERP.BLL.DTOs
{
    public class AuditAnomalyFlag
    {
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string AnomalyType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium";
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public int RelatedLogCount { get; set; }
    }
}
