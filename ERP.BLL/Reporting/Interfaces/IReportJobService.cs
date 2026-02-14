using ERP.BLL.Reporting.Dtos;
using ERP.DAL.Models;

namespace ERP.BLL.Reporting.Interfaces
{
    public interface IReportJobService
    {
        Task<int> EnqueueJobAsync(string userId, ReportType reportType, ReportFileFormat format, ReportRequestDto request);
        Task<IReadOnlyList<ReportJob>> GetUserJobsAsync(string userId);
        Task<IReadOnlyList<ReportPreset>> GetUserPresetsAsync(string userId);
        Task SavePresetAsync(string userId, string name, ReportType reportType, ReportRequestDto request);
        Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken);
    }
}
