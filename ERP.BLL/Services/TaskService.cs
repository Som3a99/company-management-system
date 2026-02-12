using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

        private static readonly Dictionary<TaskStatus, HashSet<TaskStatus>> AllowedTransitions = new()
        {
            [TaskStatus.New] = new HashSet<TaskStatus> { TaskStatus.InProgress, TaskStatus.Cancelled },
            [TaskStatus.InProgress] = new HashSet<TaskStatus> { TaskStatus.Blocked, TaskStatus.Completed, TaskStatus.Cancelled },
            [TaskStatus.Blocked] = new HashSet<TaskStatus> { TaskStatus.InProgress, TaskStatus.Cancelled },
            [TaskStatus.Completed] = new HashSet<TaskStatus>(),
            [TaskStatus.Cancelled] = new HashSet<TaskStatus>()
        };

        public TaskService(
            ITaskRepository taskRepository,
            IProjectRepository projectRepository,
            IEmployeeRepository employeeRepository,
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor)
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _employeeRepository = employeeRepository;
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<TaskOperationResult<TaskItem>> CreateTaskAsync(CreateTaskRequest request, string currentUserId)
        {
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

            return TaskOperationResult<TaskItem>.Success(entity);
        }

        public async Task<TaskOperationResult> UpdateTaskStatusAsync(UpdateTaskStatusRequest request, string currentUserId)
        {
            var task = await _taskRepository.GetTaskWithScopeDataAsync(request.TaskId);
            if (task == null)
                return TaskOperationResult.NotFound();

            if (!CanUpdateTaskStatus(task, currentUserId))
                return TaskOperationResult.Forbidden();

            if (!IsValidTransition(task.Status, request.NewStatus))
                return TaskOperationResult.Invalid("Invalid status transition.");

            task.Status = request.NewStatus;
            task.UpdatedAt = DateTime.UtcNow;
            task.CompletedAt = request.NewStatus == TaskStatus.Completed ? DateTime.UtcNow : null;

            try
            {
                await _unitOfWork.CompleteAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
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

            if (task.ProjectId == null)
                return TaskOperationResult.Invalid("Task is not associated with a project.");

            if (!await _taskRepository.IsEmployeeAssignedToProjectAsync(request.AssignedToEmployeeId, task.ProjectId.Value))
                return TaskOperationResult.Invalid("Employee must be assigned to the same project.");

            task.AssignedToEmployeeId = request.AssignedToEmployeeId;
            task.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.CompleteAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return TaskOperationResult.Invalid("Task was modified by another user. Please reload and retry.");
            }

            return TaskOperationResult.Success();
        }

        public async Task<TaskOperationResult> LogActualHoursAsync(LogTaskHoursRequest request, string currentUserId)
        {
            if (request.AdditionalHours <= 0)
                return TaskOperationResult.Invalid("Hours must be greater than zero.");

            var task = await _taskRepository.GetTaskWithScopeDataAsync(request.TaskId);
            if (task == null)
                return TaskOperationResult.NotFound();

            if (!CanUpdateOwnTask(task, currentUserId) && !CanManageProject(task.Project))
                return TaskOperationResult.Forbidden();

            task.ActualHours += request.AdditionalHours;
            task.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.CompleteAsync();
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

            if (!CanUpdateOwnTask(task, currentUserId) && !CanManageProject(task.Project) && !(_httpContextAccessor.HttpContext?.User.IsInRole("CEO") ?? false))
                return TaskOperationResult<TaskComment>.Forbidden();

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

            return TaskOperationResult<TaskComment>.Success(comment);
        }

        public async Task<IEnumerable<TaskItem>> GetVisibleTasksAsync(string currentUserId)
        {
            return await _taskRepository.GetVisibleTasksAsync(currentUserId);
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

            var managerEmployeeIdClaim = user.FindFirst("EmployeeId")?.Value;
            if (int.TryParse(managerEmployeeIdClaim, out var managerEmployeeId))
                return project.ProjectManagerId == managerEmployeeId;

            return false;
        }

        private bool CanUpdateOwnTask(TaskItem task, string currentUserId)
        {
            return task.AssignedToEmployee.ApplicationUserId == currentUserId;
        }

        private bool CanUpdateTaskStatus(TaskItem task, string currentUserId)
        {
            if (_httpContextAccessor.HttpContext?.User.IsInRole("CEO") == true)
                return true;

            if (CanManageProject(task.Project))
                return true;

            return CanUpdateOwnTask(task, currentUserId);
        }
    }
}
