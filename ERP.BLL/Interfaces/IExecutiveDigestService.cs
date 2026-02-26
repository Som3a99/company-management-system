using ERP.BLL.DTOs;

namespace ERP.BLL.Interfaces
{
    public interface IExecutiveDigestService
    {
        Task<WeeklyDigestData> PrepareDigestAsync();
    }
}
