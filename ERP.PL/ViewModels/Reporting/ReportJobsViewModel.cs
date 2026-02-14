using ERP.DAL.Models;

namespace ERP.PL.ViewModels.Reporting
{
    public sealed class ReportJobsViewModel
    {
        public IReadOnlyList<ReportJob> Jobs { get; set; } = [];
        public IReadOnlyList<ReportPreset> Presets { get; set; } = [];
    }

    public sealed class ReportJobRequestViewModel
    {
        public ReportType ReportType { get; set; }
        public ReportFileFormat Format { get; set; }
        public ReportFilterViewModel Filters { get; set; } = new();
    }

    public sealed class ReportPresetCreateViewModel
    {
        public string Name { get; set; } = string.Empty;
        public ReportType ReportType { get; set; }
        public ReportFilterViewModel Filters { get; set; } = new();
    }

    public sealed class ReportWidgetViewModel
    {
        public int TotalTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
    }
}
