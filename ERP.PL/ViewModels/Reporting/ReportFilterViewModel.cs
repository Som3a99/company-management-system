namespace ERP.PL.ViewModels.Reporting
{
    public enum ReportExportFormat
    {
        Html = 0,
        Csv = 1,
        Pdf = 2,
        Excel = 3
    }

    public sealed class ReportFilterViewModel
    {
        public DateTime? StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }
        public int? DepartmentId { get; set; }
        public int? ProjectId { get; set; }
        public ReportExportFormat Export { get; set; } = ReportExportFormat.Html;
    }
}
