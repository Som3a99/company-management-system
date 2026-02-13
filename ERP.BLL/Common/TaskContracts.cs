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


    public record UpdateTaskRequest(
    int TaskId,
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTime? DueDate,
    DateTime? StartDate,
    decimal? EstimatedHours,
    byte[]? RowVersion = null);

    public record UpdateTaskStatusRequest(int TaskId, TaskStatus NewStatus, byte[]? RowVersion = null);
    public record ReassignTaskRequest(int TaskId, int AssignedToEmployeeId, byte[]? RowVersion = null);
    public record UnassignTaskRequest(int TaskId, byte[]? RowVersion = null);
    public record AddTaskCommentRequest(int TaskId, string Content);

    public record TaskQueryRequest(
    int PageNumber = 1,
    int PageSize = 10,
    int? ProjectId = null,
    TaskStatus? Status = null,
    int? AssigneeEmployeeId = null,
    string? SortBy = null,
    bool Descending = false);
}
