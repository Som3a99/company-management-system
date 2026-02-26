using ERP.BLL.DTOs;

namespace ERP.BLL.Interfaces
{
    public interface IDashboardIntelligenceService
    {
        Task<DashboardIntelligenceData> GetIntelligenceAsync();
    }
}
