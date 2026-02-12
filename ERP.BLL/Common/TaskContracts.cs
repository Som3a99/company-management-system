using ERP.DAL.Models;
using TaskStatus = ERP.DAL.Models.TaskStatus;
namespace ERP.BLL.Common
{
    public record CreateTaskRequest(
        string Title,
        string? Description,
        int ProjectId,
        int AssignedToEmployeeId,
        TaskPriority Priority,
        DateTime? DueDate,
        DateTime? StartDate,
        decimal? EstimatedHours);

    public record UpdateTaskStatusRequest(int TaskId, TaskStatus NewStatus);
    public record ReassignTaskRequest(int TaskId, int AssignedToEmployeeId);
    public record LogTaskHoursRequest(int TaskId, decimal AdditionalHours);
    public record AddTaskCommentRequest(int TaskId, string Content);
}
