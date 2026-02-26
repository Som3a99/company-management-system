using ERP.BLL.DTOs;

namespace ERP.BLL.Interfaces
{
    public interface ITaskDescriptionService
    {
        Task<string> GenerateDescriptionAsync(GenerateTaskDescriptionRequest request);
    }
}
