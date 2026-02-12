using ERP.DAL.Models;

namespace ERP.BLL.Interfaces
{
    public interface ITaskRepository : IGenericRepository<TaskItem>
    {
        Task<TaskItem?> GetTaskWithScopeDataAsync(int taskId);
        Task<bool> IsEmployeeAssignedToProjectAsync(int employeeId, int projectId);
        Task<IEnumerable<TaskItem>> GetVisibleTasksAsync(string userId);
    }
}
