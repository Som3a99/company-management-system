using ERP.BLL.DTOs;

namespace ERP.BLL.Interfaces
{
    public interface ITeamHealthService
    {
        Task<TeamHealthScoreResult> CalculateAsync();
    }
}
