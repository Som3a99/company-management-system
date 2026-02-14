using ERP.BLL.Reporting.Dtos;

namespace ERP.PL.ViewModels.Reporting
{
    public sealed class ReportingIndexViewModel
    {
        public ReportFilterViewModel Filters { get; set; } = new();
        public IReadOnlyList<TaskReportRowDto> TaskRows { get; set; } = [];
        public IReadOnlyList<ProjectReportRowDto> ProjectRows { get; set; } = [];
        public IReadOnlyList<DepartmentReportRowDto> DepartmentRows { get; set; } = [];
        public IReadOnlyList<AuditReportRowDto> AuditRows { get; set; } = [];
        public ReportWidgetViewModel Widget { get; set; } = new();
    }
}
