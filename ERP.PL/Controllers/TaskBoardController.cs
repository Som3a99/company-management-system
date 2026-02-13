using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.PL.ViewModels.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.PL.Controllers
{
    [Authorize]
    public class TaskBoardController : Controller
    {
        private readonly ITaskService _taskService;

        public TaskBoardController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            int pageNumber = 1,
            int pageSize = 10,
            int? projectId = null,
            TaskStatus? status = null,
            int? assigneeEmployeeId = null,
            string? sortBy = "duedate",
            bool descending = false)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var request = new TaskQueryRequest(pageNumber, pageSize, projectId, status, assigneeEmployeeId, sortBy, descending);
            var paged = await _taskService.GetTasksForUserAsync(request, userId);

            var newCount = (await _taskService.GetTasksForUserAsync(request with { Status = TaskStatus.New, PageNumber = 1, PageSize = 1 }, userId)).TotalCount;
            var inProgressCount = (await _taskService.GetTasksForUserAsync(request with { Status = TaskStatus.InProgress, PageNumber = 1, PageSize = 1 }, userId)).TotalCount;
            var blockedCount = (await _taskService.GetTasksForUserAsync(request with { Status = TaskStatus.Blocked, PageNumber = 1, PageSize = 1 }, userId)).TotalCount;
            var completedCount = (await _taskService.GetTasksForUserAsync(request with { Status = TaskStatus.Completed, PageNumber = 1, PageSize = 1 }, userId)).TotalCount;

            var vm = new TaskBoardIndexViewModel
            {
                Tasks = paged.Items.ToList(),
                PageNumber = paged.PageNumber,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount,
                ProjectId = projectId,
                Status = status,
                AssigneeEmployeeId = assigneeEmployeeId,
                SortBy = sortBy,
                Descending = descending,
                NewCount = newCount,
                InProgressCount = inProgressCount,
                BlockedCount = blockedCount,
                CompletedCount = completedCount
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var task = await _taskService.GetTaskByIdAsync(id, userId);
            if (task == null)
                return NotFound();

            return View(new TaskDetailsViewModel
            {
                Task = task,
                NewStatus = task.Status,
                RowVersionBase64 = Convert.ToBase64String(task.RowVersion)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, TaskStatus newStatus, string? rowVersion)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var result = await _taskService.UpdateTaskStatusAsync(
                new UpdateTaskStatusRequest(id, newStatus, ParseRowVersion(rowVersion)),
                userId);

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Error ?? "Unable to update task status.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["SuccessMessage"] = "Task status updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int id, string content)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var result = await _taskService.AddCommentAsync(new AddTaskCommentRequest(id, content), userId);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Error ?? "Unable to add comment.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["SuccessMessage"] = "Comment added successfully.";
            return RedirectToAction(nameof(Details), new { id });
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
    }
}
