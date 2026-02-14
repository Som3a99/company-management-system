namespace ERP.DAL.Models
{
    public class ReportPreset : Base
    {
        public string UserId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public ReportType ReportType { get; set; }
        public string FiltersJson { get; set; } = "{}";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
