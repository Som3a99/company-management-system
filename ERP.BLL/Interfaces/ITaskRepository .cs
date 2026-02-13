using ERP.BLL.Common;
using ERP.DAL.Models;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.BLL.Interfaces
{
    public interface ITaskRepository : IGenericRepository<TaskItem>
    {
        Task<TaskItem?> GetTaskWithScopeDataAsync(int taskId);
        Task<TaskItem?> GetTaskByIdWithDetailsAsync(int taskId);
        Task<bool> IsEmployeeAssignedToProjectAsync(int employeeId, int projectId);
        Task<PagedResult<TaskItem>> GetTasksPagedAsync(
            int pageNumber,
            int pageSize,
            int? projectId,
            TaskStatus? status,
            int? assigneeEmployeeId,
            string? sortBy,
            bool descending,
            string currentUserId);
    }
}
