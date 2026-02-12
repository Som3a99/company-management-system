using ERP.BLL.Common;
using ERP.DAL.Models;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.BLL.Interfaces
{
    public interface ITaskService
    {
        Task<TaskOperationResult<TaskItem>> CreateTaskAsync(CreateTaskRequest request, string currentUserId);
        Task<TaskOperationResult> UpdateTaskStatusAsync(UpdateTaskStatusRequest request, string currentUserId);
        Task<TaskOperationResult> ReassignTaskAsync(ReassignTaskRequest request, string currentUserId);
        Task<TaskOperationResult> LogActualHoursAsync(LogTaskHoursRequest request, string currentUserId);
        Task<TaskOperationResult<TaskComment>> AddCommentAsync(AddTaskCommentRequest request, string currentUserId);
        Task<IEnumerable<TaskItem>> GetVisibleTasksAsync(string currentUserId);
        bool IsValidTransition(TaskStatus oldStatus, TaskStatus newStatus);
    }
}
