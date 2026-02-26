using ERP.BLL.DTOs;

namespace ERP.BLL.Interfaces
{
    public interface IAiNarrativeService
    {
        Task<string> GenerateSummaryAsync(ReportSummaryInput input);
    }
}
