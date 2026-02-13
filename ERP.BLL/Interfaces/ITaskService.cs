using ERP.BLL.Common;
using ERP.DAL.Models;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.BLL.Interfaces
{
    public interface ITaskService
    {
        Task<TaskOperationResult<TaskItem>> CreateTaskAsync(CreateTaskRequest request, string currentUserId);
        Task<TaskItem?> GetTaskByIdAsync(int taskId, string currentUserId);
        Task<TaskOperationResult> UpdateTaskAsync(UpdateTaskRequest request, string currentUserId);
        Task<TaskOperationResult> UpdateTaskStatusAsync(UpdateTaskStatusRequest request, string currentUserId);
        Task<TaskOperationResult> ReassignTaskAsync(ReassignTaskRequest request, string currentUserId);
        Task<TaskOperationResult> UnassignTaskAsync(UnassignTaskRequest request, string currentUserId);
        Task<TaskOperationResult> DeleteTaskAsync(int taskId, byte[]? rowVersion, string currentUserId);
        Task<TaskOperationResult<TaskComment>> AddCommentAsync(AddTaskCommentRequest request, string currentUserId);
        Task<PagedResult<TaskItem>> GetTasksForUserAsync(TaskQueryRequest request, string currentUserId);
        Task<PagedResult<TaskItem>> GetTasksForProjectManagerAsync(TaskQueryRequest request, string currentUserId);
        bool IsValidTransition(TaskStatus oldStatus, TaskStatus newStatus);
    }
}
