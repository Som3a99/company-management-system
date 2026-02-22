using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.BLL.Services
{
    public class TaskService : ITaskService
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _dbContext;
        private readonly ICacheService _cacheService;

        private static readonly Dictionary<TaskStatus, HashSet<TaskStatus>> AllowedTransitions = new()
        {
            [TaskStatus.New] = new HashSet<TaskStatus> { TaskStatus.InProgress, TaskStatus.Cancelled },
            [TaskStatus.Blocked] = new HashSet<TaskStatus> { TaskStatus.InProgress, TaskStatus.Cancelled },
            [TaskStatus.InProgress] = new HashSet<TaskStatus> { TaskStatus.Blocked, TaskStatus.Completed },
            [TaskStatus.Blocked] = new HashSet<TaskStatus> { TaskStatus.InProgress },
            [TaskStatus.Completed] = new HashSet<TaskStatus>(),
            [TaskStatus.Cancelled] = new HashSet<TaskStatus>()
        };

        public TaskService(
            ITaskRepository taskRepository,
            IProjectRepository projectRepository,
            IEmployeeRepository employeeRepository,
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor,
            ApplicationDbContext dbContext,
            ICacheService cacheService)
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _employeeRepository = employeeRepository;
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _dbContext = dbContext;
            _cacheService=cacheService;
        }

        public async Task<TaskOperationResult<TaskItem>> CreateTaskAsync(CreateTaskRequest request, string currentUserId)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return TaskOperationResult<TaskItem>.Invalid("Task title is required.");

            if (request.DueDate.HasValue && request.StartDate.HasValue && request.DueDate.Value < request.StartDate.Value)
                return TaskOperationResult<TaskItem>.Invalid("Due date cannot be before start date.");

            if (request.EstimatedHours.HasValue && request.EstimatedHours.Value < 0)
                return TaskOperationResult<TaskItem>.Invalid("Estimated hours cannot be negative.");

            var project = await _projectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
                return TaskOperationResult<TaskItem>.NotFound("Project not found.");

            if (!CanManageProject(project))
                return TaskOperationResult<TaskItem>.Forbidden();

            var assignee = await _employeeRepository.GetByIdAsync(request.AssignedToEmployeeId);
            if (assignee == null)
                return TaskOperationResult<TaskItem>.NotFound("Assigned employee not found.");

            if (!await _taskRepository.IsEmployeeAssignedToProjectAsync(request.AssignedToEmployeeId, request.ProjectId))
                return TaskOperationResult<TaskItem>.Invalid("Employee must be assigned to the same project.");

            var entity = new TaskItem
            {
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                ProjectId = request.ProjectId,
                AssignedToEmployeeId = request.AssignedToEmployeeId,
                CreatedByUserId = currentUserId,
                Priority = request.Priority,
                Status = TaskStatus.New,
                DueDate = request.DueDate,
                StartDate = request.StartDate,
                EstimatedHours = request.EstimatedHours,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ActualHours = 0
            };

            await _taskRepository.AddAsync(entity);
            await _unitOfWork.CompleteAsync();
            await InvalidateTaskReportCachesAsync();
            await WriteAuditLogAsync(currentUserId, "TASK_CREATE", entity.Id, new { entity.ProjectId, entity.AssignedToEmployeeId, entity.Priority });

            return TaskOperationResult<TaskItem>.Success(entity);
        }

        public async Task<TaskItem?> GetTaskByIdAsync(int taskId, string currentUserId)
        {
            var task = await _taskRepository.GetTaskByIdWithDetailsAsync(taskId);
            if (task == null)
                return null;

            return CanViewTask(task, currentUserId) ? task : null;
        }

        public async Task<TaskOperationResult> UpdateTaskAsync(UpdateTaskRequest request, string currentUserId)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return TaskOperationResult.Invalid("Task title is required.");

            if (request.DueDate.HasValue && request.StartDate.HasValue && request.DueDate.Value < request.StartDate.Value)
                return TaskOperationResult.Invalid("Due date cannot be before start date.");

            if (request.EstimatedHours.HasValue && request.EstimatedHours.Value < 0)
                return TaskOperationResult.Invalid("Estimated hours cannot be negative.");

            var task = await _taskRepository.GetTaskWithScopeDataAsync(request.TaskId);
            if (task == null)
                return TaskOperationResult.NotFound();

            if (!CanManageProject(task.Project) && !(_httpContextAccessor.HttpContext?.User.IsInRole("CEO") ?? false))
                return TaskOperationResult.Forbidden();

            if (IsTaskLocked(task))
                return TaskOperationResult.Invalid("Completed or closed tasks cannot be edited.");

            if (request.RowVersion != null)
                _dbContext.Entry(task).Property(t => t.RowVersion).OriginalValue = request.RowVersion;

            task.Title = request.Title.Trim();
            task.Description = request.Description?.Trim();
            task.Priority = request.Priority;
            task.StartDate = request.StartDate;
            task.DueDate = request.DueDate;
            task.EstimatedHours = request.EstimatedHours;
            task.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.CompleteAsync();
                await InvalidateTaskReportCachesAsync();
                await WriteAuditLogAsync(currentUserId, "TASK_UPDATE", task.Id, new { task.Title, task.Priority, task.DueDate, task.StartDate });
            }
            catch (DbUpdateConcurrencyException)
            {
                return TaskOperationResult.Invalid("Task was modified by another user. Please reload and retry.");
            }

            return TaskOperationResult.Success();
        }

        public async Task<TaskOperationResult> UpdateTaskStatusAsync(UpdateTaskStatusRequest request, string currentUserId)
        {
            var task = await _taskRepository.GetTaskWithScopeDataAsync(request.TaskId);
            if (task == null)
                return TaskOperationResult.NotFound();

            if (!CanUpdateTaskStatus(task, currentUserId))
                return TaskOperationResult.Forbidden();

            if (IsTaskLocked(task))
                return TaskOperationResult.Invalid("Completed or closed tasks cannot be edited.");

            if (!IsValidTransition(task.Status, request.NewStatus))
                return TaskOperationResult.Invalid("Invalid status transition.");


            if (request.RowVersion != null)
                _dbContext.Entry(task).Property(t => t.RowVersion).OriginalValue = request.RowVersion;

            var oldStatus = task.Status;

            task.Status = request.NewStatus;
            task.UpdatedAt = DateTime.UtcNow;
            task.CompletedAt = request.NewStatus == TaskStatus.Completed ? DateTime.UtcNow : null;

            try
            {
                await _unitOfWork.CompleteAsync();
                await InvalidateTaskReportCachesAsync();
                await WriteAuditLogAsync(currentUserId, "TASK_STATUS_UPDATE", task.Id, new { OldStatus = oldStatus.ToString(), NewStatus = request.NewStatus.ToString() });
            }
            catch (DbUpdateConcurrencyException)
            {
                await WriteAuditLogAsync(currentUserId, "TASK_STATUS_UPDATE_DENIED", task.Id, new { Reason = "ConcurrencyConflict" }, false, "Task was modified by another user.");
                return TaskOperationResult.Invalid("Task was modified by another user. Please reload and retry.");
            }

            return TaskOperationResult.Success();
        }

        public async Task<TaskOperationResult> ReassignTaskAsync(ReassignTaskRequest request, string currentUserId)
        {
            var task = await _taskRepository.GetTaskWithScopeDataAsync(request.TaskId);
            if (task == null)
                return TaskOperationResult.NotFound();

            if (!CanManageProject(task.Project))
                return TaskOperationResult.Forbidden();

            if (IsTaskLocked(task))
                return TaskOperationResult.Invalid("Completed or closed tasks cannot be edited.");

            if (task.ProjectId == null)
                return TaskOperationResult.Invalid("Task is not associated with a project.");

            if (!await _taskRepository.IsEmployeeAssignedToProjectAsync(request.AssignedToEmployeeId, task.ProjectId.Value))
                return TaskOperationResult.Invalid("Employee must be assigned to the same project.");

            if (request.RowVersion != null)
                _dbContext.Entry(task).Property(t => t.RowVersion).OriginalValue = request.RowVersion;

            var previousAssignee = task.AssignedToEmployeeId;
            task.AssignedToEmployeeId = request.AssignedToEmployeeId;
            task.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.CompleteAsync();
                await InvalidateTaskReportCachesAsync();
                await WriteAuditLogAsync(currentUserId, "TASK_REASSIGN", task.Id, new { OldAssigneeEmployeeId = previousAssignee, NewAssigneeEmployeeId = request.AssignedToEmployeeId });
            }
            catch (DbUpdateConcurrencyException)
            {
                return TaskOperationResult.Invalid("Task was modified by another user. Please reload and retry.");
            }

            return TaskOperationResult.Success();
        }

        public async Task<TaskOperationResult> UnassignTaskAsync(UnassignTaskRequest request, string currentUserId)
        {
            var task = await _taskRepository.GetTaskWithScopeDataAsync(request.TaskId);
            if (task == null)
                return TaskOperationResult.NotFound();

            if (!CanManageProject(task.Project) && !(_httpContextAccessor.HttpContext?.User.IsInRole("CEO") ?? false))
                return TaskOperationResult.Forbidden();


            if (IsTaskLocked(task))
                return TaskOperationResult.Invalid("Completed or closed tasks cannot be edited.");

            if (request.RowVersion != null)
                _dbContext.Entry(task).Property(t => t.RowVersion).OriginalValue = request.RowVersion;

            task.AssignedToEmployeeId = null;
            task.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.CompleteAsync();
                await InvalidateTaskReportCachesAsync();
                await WriteAuditLogAsync(currentUserId, "TASK_UNASSIGN", task.Id, new { });
            }
            catch (DbUpdateConcurrencyException)
            {
                return TaskOperationResult.Invalid("Task was modified by another user. Please reload and retry.");
            }

            return TaskOperationResult.Success();
        }

        public async Task<TaskOperationResult> DeleteTaskAsync(int taskId, byte[]? rowVersion, string currentUserId)
        {
            var task = await _taskRepository.GetTaskWithScopeDataAsync(taskId);
            if (task == null)
                return TaskOperationResult.NotFound();

            if (!CanManageProject(task.Project) && !(_httpContextAccessor.HttpContext?.User.IsInRole("CEO") ?? false))
                return TaskOperationResult.Forbidden();

            if (rowVersion != null)
                _dbContext.Entry(task).Property(t => t.RowVersion).OriginalValue = rowVersion;

            _dbContext.TaskItems.Remove(task);

            try
            {
                await _unitOfWork.CompleteAsync();
                await InvalidateTaskReportCachesAsync();
                await WriteAuditLogAsync(currentUserId, "TASK_DELETE", taskId, new { });
            }
            catch (DbUpdateConcurrencyException)
            {
                return TaskOperationResult.Invalid("Task was modified by another user. Please reload and retry.");
            }

            return TaskOperationResult.Success();
        }

        public async Task<TaskOperationResult<TaskComment>> AddCommentAsync(AddTaskCommentRequest request, string currentUserId)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return TaskOperationResult<TaskComment>.Invalid("Comment content is required.");

            var task = await _taskRepository.GetTaskWithScopeDataAsync(request.TaskId);
            if (task == null)
                return TaskOperationResult<TaskComment>.NotFound();

            if (!CanViewTask(task, currentUserId))
                return TaskOperationResult<TaskComment>.Forbidden();

            if (IsTaskLocked(task))
                return TaskOperationResult<TaskComment>.Invalid("Completed or closed tasks cannot be edited.");

            if (request.MentionedEmployeeIds != null && request.MentionedEmployeeIds.Count > 0)
            {
                if (!task.ProjectId.HasValue)
                    return TaskOperationResult<TaskComment>.Invalid("Mentions require a task project.");

                var mentionedIds = request.MentionedEmployeeIds.Where(id => id > 0).Distinct().ToList();
                foreach (var mentionedId in mentionedIds)
                {
                    if (!await _taskRepository.IsEmployeeAssignedToProjectAsync(mentionedId, task.ProjectId.Value))
                        return TaskOperationResult<TaskComment>.Invalid("Mentioned users must belong to the same project and task scope.");
                }
            }

            var comment = new TaskComment
            {
                TaskId = request.TaskId,
                UserId = currentUserId,
                Content = request.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            task.Comments.Add(comment);
            task.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.CompleteAsync();
            await WriteAuditLogAsync(currentUserId, "TASK_COMMENT_ADD", task.Id, new { CommentId = comment.Id });
            return TaskOperationResult<TaskComment>.Success(comment);
        }

        public Task<PagedResult<TaskItem>> GetTasksForUserAsync(TaskQueryRequest request, string currentUserId)
        {
            return _taskRepository.GetTasksPagedAsync(
                request.PageNumber,
                request.PageSize,
                request.ProjectId,
                request.Status,
                request.AssigneeEmployeeId,
                request.SortBy,
                request.Descending,
                currentUserId);
        }

        public Task<PagedResult<TaskItem>> GetTasksForProjectManagerAsync(TaskQueryRequest request, string currentUserId)
        {
            return GetTasksForUserAsync(request, currentUserId);
        }

        public bool IsValidTransition(TaskStatus oldStatus, TaskStatus newStatus)
        {
            if (oldStatus == newStatus)
                return true;

            return AllowedTransitions.TryGetValue(oldStatus, out var allowed) && allowed.Contains(newStatus);
        }

        private bool CanManageProject(Project project)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
                return false;

            if (user.IsInRole("CEO"))
                return true;

            if (!user.IsInRole("ProjectManager"))
                return false;

            var managerEmployeeIdClaim = user.FindFirst("EmployeeId")?.Value;
            if (int.TryParse(managerEmployeeIdClaim, out var managerEmployeeId))
                return project.ProjectManagerId == managerEmployeeId;

            return false;
        }

        private bool CanViewTask(TaskItem task, string currentUserId)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
                return false;

            if (user.IsInRole("CEO"))
                return true;

            if (CanManageProject(task.Project))
                return true;

            var managedDepartmentIdClaim = user.FindFirst("ManagedDepartmentId")?.Value;
            if (int.TryParse(managedDepartmentIdClaim, out var managedDepartmentId) && task.AssignedToEmployee?.DepartmentId == managedDepartmentId)
                return true;

            return task.AssignedToEmployee?.ApplicationUserId == currentUserId;
        }

        private bool CanUpdateTaskStatus(TaskItem task, string currentUserId)
        {
            if (_httpContextAccessor.HttpContext?.User.IsInRole("CEO") == true)
                return true;

            if (CanManageProject(task.Project))
                return true;

            return task.AssignedToEmployee?.ApplicationUserId == currentUserId;
        }

        private static bool IsTaskLocked(TaskItem task)
        {
            return task.Status == TaskStatus.Completed || task.Status == TaskStatus.Cancelled;
        }

        private async Task WriteAuditLogAsync(string userId, string action, int taskId, object details, bool succeeded = true, string? errorMessage = null)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var audit = new AuditLog
            {
                UserId = userId,
                UserEmail = user?.Identity?.Name ?? "unknown",
                Action = action,
                ResourceType = "Task",
                ResourceId = taskId,
                Details = JsonSerializer.Serialize(details),
                Succeeded = succeeded,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow,
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString()
            };

            _dbContext.AuditLogs.Add(audit);
            await _dbContext.SaveChangesAsync();

        }
        /// <summary>
        /// Invalidation rule: task writes impact task/project/department reporting aggregates.
        /// </summary>
        private Task InvalidateTaskReportCachesAsync()
        {
            return Task.WhenAll(
                _cacheService.RemoveByPrefixAsync(CacheKeys.ReportTasksPrefix),
                _cacheService.RemoveByPrefixAsync(CacheKeys.ReportProjectsPrefix),
                _cacheService.RemoveByPrefixAsync(CacheKeys.ReportDepartmentsPrefix));
        }
    }
}
