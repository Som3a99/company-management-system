using ERP.BLL.DTOs;

namespace ERP.BLL.Interfaces
{
    public interface IProjectForecastService
    {
        Task<ProjectForecastResult?> ForecastAsync(int projectId);
    }
}
