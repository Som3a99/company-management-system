using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.PL.Controllers
{
    [ApiController]
    [Authorize]
    [Route("tasks")]
    public class TasksController : ControllerBase
    {
        private readonly ITaskService _taskService;

        public TasksController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        [HttpPost]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var request = new CreateTaskRequest(
                dto.Title,
                dto.Description,
                dto.ProjectId,
                dto.AssignedToEmployeeId,
                dto.Priority,
                dto.DueDate,
                dto.StartDate,
                dto.EstimatedHours);

            var result = await _taskService.CreateTaskAsync(request, userId);
            if (!result.Succeeded)
                return MapError(result.Error);

            return CreatedAtAction(nameof(GetTask), new { id = result.Data!.Id }, ToResponse(result.Data!));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTask(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var task = await _taskService.GetTaskByIdAsync(id, userId);
            if (task == null)
                return NotFound();

            return Ok(ToResponse(task));
        }


        [HttpPut("{id:int}")]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var request = new UpdateTaskRequest(id, dto.Title, dto.Description, dto.Priority, dto.DueDate, dto.StartDate, dto.EstimatedHours, ParseRowVersion(dto.RowVersion));
            var result = await _taskService.UpdateTaskAsync(request, userId);
            return result.Succeeded ? NoContent() : MapError(result.Error);
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTaskStatusDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var request = new UpdateTaskStatusRequest(id, dto.NewStatus, ParseRowVersion(dto.RowVersion));
            var result = await _taskService.UpdateTaskStatusAsync(request, userId);

            return result.Succeeded ? NoContent() : MapError(result.Error);
        }

        [HttpPut("{id:int}/assignment")]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> UpdateAssignment(int id, [FromBody] ReassignTaskDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var request = new ReassignTaskRequest(id, dto.AssignedToEmployeeId, ParseRowVersion(dto.RowVersion));
            var result = await _taskService.ReassignTaskAsync(request, userId);

            return result.Succeeded ? NoContent() : MapError(result.Error);
        }


        [HttpPut("{id:int}/unassign")]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> Unassign(int id, [FromBody] RowVersionDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var request = new UnassignTaskRequest(id, ParseRowVersion(dto.RowVersion));
            var result = await _taskService.UnassignTaskAsync(request, userId);
            return result.Succeeded ? NoContent() : MapError(result.Error);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> DeleteTask(int id, [FromBody] RowVersionDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _taskService.DeleteTaskAsync(id, ParseRowVersion(dto.RowVersion), userId);
            return result.Succeeded ? NoContent() : MapError(result.Error);
        }

        [HttpPost("{id:int}/comments")]
        public async Task<IActionResult> AddComment(int id, [FromBody] AddTaskCommentDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var request = new AddTaskCommentRequest(id, dto.Content);
            var result = await _taskService.AddCommentAsync(request, userId);
            if (!result.Succeeded)
                return MapError(result.Error);

            return Ok(new { result.Data!.Id, result.Data.Content, result.Data.CreatedAt });
        }

        [HttpGet]
        public async Task<IActionResult> ListTasks([FromQuery] TaskQueryDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var request = new TaskQueryRequest(dto.PageNumber, dto.PageSize, dto.ProjectId, dto.Status, dto.AssigneeEmployeeId, dto.SortBy, dto.Descending);
            var result = await _taskService.GetTasksForUserAsync(request, userId);

            return Ok(new
            {
                result.PageNumber,
                result.PageSize,
                result.TotalCount,
                Items = result.Items.Select(ToResponse)
            });
        }

        private string? GetCurrentUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        private static byte[]? ParseRowVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            try
            {
                return Convert.FromBase64String(value);
            }
            catch
            {
                return null;
            }
        }

        private ObjectResult MapError(string? error)
        {
            if (string.Equals(error, "Forbidden.", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status403Forbidden, new { error });

            if (string.Equals(error, "Not found.", StringComparison.OrdinalIgnoreCase) || string.Equals(error, "Project not found.", StringComparison.OrdinalIgnoreCase) || string.Equals(error, "Assigned employee not found.", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status404NotFound, new { error });

            return StatusCode(StatusCodes.Status400BadRequest, new { error });
        }

        private static object ToResponse(TaskItem t) => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.ProjectId,
            t.AssignedToEmployeeId,
            t.Priority,
            t.Status,
            t.DueDate,
            t.StartDate,
            t.CompletedAt,
            t.EstimatedHours,
            t.ActualHours,
            t.CreatedAt,
            t.UpdatedAt,
            RowVersion = Convert.ToBase64String(t.RowVersion),
            Comments = t.Comments.Select(c => new { c.Id, c.UserId, c.Content, c.CreatedAt })
        };

        public record CreateTaskDto(
            string Title,
            string? Description,
            int ProjectId,
            int AssignedToEmployeeId,
            TaskPriority Priority,
            DateTime? DueDate,
            DateTime? StartDate,
            decimal? EstimatedHours);

        public record UpdateTaskDto(string Title, string? Description, TaskPriority Priority, DateTime? DueDate, DateTime? StartDate, decimal? EstimatedHours, string? RowVersion);
        public record UpdateTaskStatusDto(TaskStatus NewStatus, string? RowVersion);
        public record ReassignTaskDto(int AssignedToEmployeeId, string? RowVersion);
        public record AddTaskCommentDto(string Content);
        public record RowVersionDto(string? RowVersion);
        public record TaskQueryDto(
            int PageNumber = 1,
            int PageSize = 10,
            int? ProjectId = null,
            TaskStatus? Status = null,
            int? AssigneeEmployeeId = null,
            string? SortBy = null,
            bool Descending = false);
    }
}
