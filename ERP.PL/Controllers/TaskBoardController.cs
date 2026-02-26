using ERP.BLL.Common;
using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.PL.Controllers
{
    [Authorize]
    public class TaskBoardController : Controller
    {
        private readonly ITaskService _taskService;
        private readonly ApplicationDbContext _context;
        private readonly ITaskRiskService _taskRiskService;
        private readonly IWorkloadService _workloadService;
        private readonly ITaskDescriptionService _taskDescriptionService;
        private readonly ITaskAssignmentSuggestionService _suggestionService;

        public TaskBoardController(
            ITaskService taskService,
            ApplicationDbContext context,
            ITaskRiskService taskRiskService,
            IWorkloadService workloadService,
            ITaskDescriptionService taskDescriptionService,
            ITaskAssignmentSuggestionService suggestionService)
        {
            _taskService = taskService;
            _context = context;
            _taskRiskService = taskRiskService;
            _workloadService = workloadService;
            _taskDescriptionService = taskDescriptionService;
            _suggestionService = suggestionService;
        }

        #region Index
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

            // Calculate risk for each task
            var taskRisks = new Dictionary<int, BLL.DTOs.TaskRiskResult>();
            foreach (var task in paged.Items)
            {
                taskRisks[task.Id] = _taskRiskService.CalculateRisk(task);
            }

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
                CompletedCount = completedCount,
                TaskRisks = taskRisks
            };

            return View(vm);
        }
        #endregion

        #region Create
        [HttpGet]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> Create(int? projectId = null)
        {
            var vm = new TaskUpsertViewModel { ProjectId = projectId ?? 0 };
            await PopulateOptionsAsync(vm, projectId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> Create(TaskUpsertViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(vm, vm.ProjectId);
                return View(vm);
            }

            if (!vm.AssignedToEmployeeId.HasValue)
            {
                ModelState.AddModelError(nameof(vm.AssignedToEmployeeId), "Assignee is required.");
                await PopulateOptionsAsync(vm, vm.ProjectId);
                return View(vm);
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var result = await _taskService.CreateTaskAsync(
                new CreateTaskRequest(vm.Title, vm.Description, vm.ProjectId, vm.AssignedToEmployeeId.Value, vm.Priority, vm.DueDate, vm.StartDate, vm.EstimatedHours),
                userId);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create task.");
                await PopulateOptionsAsync(vm, vm.ProjectId);
                return View(vm);
            }

            TempData["SuccessMessage"] = "Task created successfully.";
            return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
        }
        #endregion

        #region Edit
        [HttpGet]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var task = await _taskService.GetTaskByIdAsync(id, userId);
            if (task == null)
                return NotFound();

            if (task.Status == TaskStatus.Completed || task.Status == TaskStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Completed or closed tasks cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var vm = new TaskUpsertViewModel
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                ProjectId = task.ProjectId ?? 0,
                AssignedToEmployeeId = task.AssignedToEmployeeId,
                Priority = task.Priority,
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                EstimatedHours = task.EstimatedHours,
                RowVersionBase64 = Convert.ToBase64String(task.RowVersion)
            };

            await PopulateOptionsAsync(vm, vm.ProjectId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> Edit(TaskUpsertViewModel vm)
        {
            if (!vm.Id.HasValue)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(vm, vm.ProjectId);
                return View(vm);
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var updateResult = await _taskService.UpdateTaskAsync(
                new UpdateTaskRequest(vm.Id.Value, vm.Title, vm.Description, vm.Priority, vm.DueDate, vm.StartDate, vm.EstimatedHours, ParseRowVersion(vm.RowVersionBase64)),
                userId);

            if (!updateResult.Succeeded)
            {
                ModelState.AddModelError(string.Empty, updateResult.Error ?? "Unable to update task.");
                await PopulateOptionsAsync(vm, vm.ProjectId);
                return View(vm);
            }

            if (vm.AssignedToEmployeeId.HasValue)
            {
                var assignResult = await _taskService.ReassignTaskAsync(new ReassignTaskRequest(vm.Id.Value, vm.AssignedToEmployeeId.Value), userId);
                if (!assignResult.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, assignResult.Error ?? "Task updated but assignment failed.");
                    await PopulateOptionsAsync(vm, vm.ProjectId);
                    return View(vm);
                }
            }
            else
            {
                var unassignResult = await _taskService.UnassignTaskAsync(new UnassignTaskRequest(vm.Id.Value), userId);
                if (!unassignResult.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, unassignResult.Error ?? "Task updated but unassign failed.");
                    await PopulateOptionsAsync(vm, vm.ProjectId);
                    return View(vm);
                }
            }

            TempData["SuccessMessage"] = "Task updated successfully.";
            return RedirectToAction(nameof(Details), new { id = vm.Id.Value });
        }
        #endregion

        #region Delete
        [HttpGet]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var task = await _taskService.GetTaskByIdAsync(id, userId);
            if (task == null)
                return NotFound();

            return View(task);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> DeleteConfirmed(int id, string? rowVersion)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var result = await _taskService.DeleteTaskAsync(id, ParseRowVersion(rowVersion), userId);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Error ?? "Unable to delete task.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            TempData["SuccessMessage"] = "Task deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Details
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var task = await _taskService.GetTaskByIdAsync(id, userId);
            if (task == null)
                return NotFound();

            var model = new TaskDetailsViewModel
            {
                Task = task,
                NewStatus = task.Status,
                RowVersionBase64 = Convert.ToBase64String(task.RowVersion)
            };

            model.AssignableEmployees = await GetAssignableEmployeeOptionsAsync(task.ProjectId);
            return View(model);
        }
        #endregion

        #region Assign & Unassign
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> Assign(int id, int assignedToEmployeeId, string? rowVersion)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var result = await _taskService.ReassignTaskAsync(new ReassignTaskRequest(id, assignedToEmployeeId, ParseRowVersion(rowVersion)), userId);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded ? "Task assignment updated." : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> Unassign(int id, string? rowVersion)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var result = await _taskService.UnassignTaskAsync(new UnassignTaskRequest(id, ParseRowVersion(rowVersion)), userId);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded ? "Task unassigned." : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }
        #endregion

        #region UpdateStatus
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
        #endregion

        #region AddComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int id, string content, List<int>? mentionedEmployeeIds)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

            var result = await _taskService.AddCommentAsync(new AddTaskCommentRequest(id, content, mentionedEmployeeIds), userId);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Error ?? "Unable to add comment.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["SuccessMessage"] = "Comment added successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        #endregion

        #region AI Generate Description
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateDescription([FromBody] GenerateTaskDescriptionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Title))
                return BadRequest(new { error = "Title is required." });

            try
            {
                var description = await _taskDescriptionService.GenerateDescriptionAsync(request);
                return Json(new { description });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Failed to generate description." });
            }
        }
        #endregion

        #region Helper Methods
        private async Task PopulateOptionsAsync(TaskUpsertViewModel vm, int? projectId)
        {
            vm.ProjectOptions = await GetManageableProjectOptionsAsync();
            vm.AssigneeOptions = await GetAssignableEmployeeOptionsAsync(projectId);

            // Populate workload data for the assignee dropdown
            if (projectId.HasValue && projectId.Value > 0)
            {
                vm.Workloads = await _workloadService.GetWorkloadAsync(projectId.Value);

                // Phase 3 — Intelligent assignment suggestions
                try
                {
                    vm.Suggestions = await _suggestionService.GetSuggestionsAsync(
                        projectId.Value, vm.Title);
                }
                catch
                {
                    vm.Suggestions = new List<TaskAssignmentSuggestion>();
                }
            }
        }

        private async Task<List<SelectListItem>> GetManageableProjectOptionsAsync()
        {
            IQueryable<Project> query = _context.Projects.AsNoTracking().Where(p => !p.IsDeleted);

            if (!User.IsInRole("CEO"))
            {
                var employeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                if (!int.TryParse(employeeIdClaim, out var employeeId))
                    return new List<SelectListItem>();

                query = query.Where(p => p.ProjectManagerId == employeeId);
            }

            return await query
                .OrderBy(p => p.ProjectName)
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = $"{p.ProjectCode} - {p.ProjectName}" })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> GetAssignableEmployeeOptionsAsync(int? projectId)
        {
            if (!projectId.HasValue)
                return new List<SelectListItem>();

            var employeeIds = await _context.ProjectEmployees
                .Where(pe => pe.ProjectId == projectId.Value)
                .Select(pe => pe.EmployeeId)
                .Distinct()
                .ToListAsync();

            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted && e.IsActive && (employeeIds.Contains(e.Id) || e.ProjectId == projectId.Value))
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = $"{e.FirstName} {e.LastName} (#{e.Id})"
                })
                .ToListAsync();

            return employees;
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
        #endregion
    }
}
