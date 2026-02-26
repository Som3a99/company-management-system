using ERP.BLL.DTOs;

namespace ERP.BLL.Interfaces
{
    public interface IWorkloadService
    {
        Task<List<EmployeeWorkloadResult>> GetWorkloadAsync(int projectId);
    }
}
