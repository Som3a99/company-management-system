using ERP.BLL.DTOs;
using ERP.BLL.Reporting.Dtos;

namespace ERP.BLL.Reporting.Interfaces
{
    public interface IReportingService
    {
        Task<IReadOnlyList<TaskReportRowDto>> GetTaskReportAsync(ReportRequestDto request, int? scopedDepartmentId, int? scopedProjectId);
        Task<IReadOnlyList<ProjectReportRowDto>> GetProjectReportAsync(ReportRequestDto request, int? scopedDepartmentId, int? scopedProjectId);
        Task<IReadOnlyList<DepartmentReportRowDto>> GetDepartmentReportAsync(ReportRequestDto request, int? scopedDepartmentId);
        Task<IReadOnlyList<AuditReportRowDto>> GetAuditReportAsync(ReportRequestDto request);
        Task<IReadOnlyList<EmployeeEstimateAccuracy>> GetEstimateAccuracyAsync(int? scopedDepartmentId);
    }
}
