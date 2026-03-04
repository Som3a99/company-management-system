using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Controllers
{
    [Authorize]
    public class EmployeeHomeController : Controller
    {
        private readonly ILogger<EmployeeHomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public EmployeeHomeController(
            ILogger<EmployeeHomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var employee = await _context.Employees
                .Include(e => e.Department)
                    .ThenInclude(d => d!.Manager)
                .Include(e => e.ProjectEmployees)
                    .ThenInclude(pe => pe.Project)
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUser.Id && !e.IsDeleted);

            var viewModel = new EmployeeHomeDashboardViewModel
            {
                UserDisplayName = currentUser.UserName ?? "User",
                EmployeeId = employee?.Id ?? 0,
                Position = employee?.Position ?? "N/A"
            };

            if (employee != null)
            {
                // ── Department Info ──
                if (employee.Department != null)
                {
                    viewModel.DepartmentName = employee.Department.DepartmentName;
                    if (employee.Department.Manager != null)
                    {
                        viewModel.ManagerName = $"{employee.Department.Manager.FirstName} {employee.Department.Manager.LastName}";
                    }
                }

                // ── My Projects ──
                var myProjectIds = employee.ProjectEmployees
                    .Select(pe => pe.ProjectId)
                    .ToList();

                // Also include direct project assignment
                if (employee.ProjectId.HasValue && !myProjectIds.Contains(employee.ProjectId.Value))
                    myProjectIds.Add(employee.ProjectId.Value);

                var projects = await _context.Projects
                    .Where(p => myProjectIds.Contains(p.Id) && !p.IsDeleted)
                    .Select(p => new EmployeeProjectSummary
                    {
                        Id = p.Id,
                        ProjectName = p.ProjectName,
                        Status = p.Status.ToString(),
                        StatusBadgeClass = GetProjectStatusBadge(p.Status),
                        TaskCount = p.Tasks.Count(t => t.AssignedToEmployeeId == employee.Id)
                    })
                    .ToListAsync();

                viewModel.MyProjects = projects;

                // ── My Tasks ──
                var now = DateTime.UtcNow;
                var dueSoonThreshold = now.AddDays(3);

                var allMyTasks = await _context.TaskItems
                    .Include(t => t.Project)
                    .Where(t => t.AssignedToEmployeeId == employee.Id
                        && t.Status != ERP.DAL.Models.TaskStatus.Cancelled)
                    .OrderByDescending(t => t.Priority)
                    .ThenBy(t => t.DueDate)
                    .Take(50)
                    .ToListAsync();

                var activeTasks = allMyTasks
                    .Where(t => t.Status != ERP.DAL.Models.TaskStatus.Completed)
                    .ToList();

                viewModel.AssignedTasks = activeTasks
                    .Take(10)
                    .Select(MapToEmployeeTaskItem)
                    .ToList();

                viewModel.OverdueTasks = activeTasks
                    .Where(t => t.DueDate.HasValue && t.DueDate.Value < now)
                    .Take(10)
                    .Select(MapToEmployeeTaskItem)
                    .ToList();

                viewModel.DueSoonTasks = activeTasks
                    .Where(t => t.DueDate.HasValue && t.DueDate.Value >= now && t.DueDate.Value <= dueSoonThreshold)
                    .Take(10)
                    .Select(MapToEmployeeTaskItem)
                    .ToList();

                // ── Personal Task Summary ──
                viewModel.TotalAssignedTasks = allMyTasks.Count;
                viewModel.CompletedTasks = allMyTasks.Count(t => t.Status == ERP.DAL.Models.TaskStatus.Completed);
                viewModel.InProgressTasks = allMyTasks.Count(t => t.Status == ERP.DAL.Models.TaskStatus.InProgress);
                viewModel.OverdueCount = viewModel.OverdueTasks.Count;
            }

            // ── Recent Notifications ──
            try
            {
                var notifications = await _notificationService.GetForUserAsync(currentUser.Id, take: 5);
                viewModel.RecentNotifications = notifications.Select(n => new NotificationItem
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    Severity = n.Severity.ToString().ToLower(),
                    LinkUrl = n.LinkUrl,
                    IsRead = n.IsRead,
                    TimeAgo = GetTimeAgo(n.CreatedAt)
                }).ToList();
                viewModel.UnreadNotificationCount = await _notificationService.GetUnreadCountAsync(currentUser.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load notifications for employee home");
            }

            return View(viewModel);
        }

        private static EmployeeTaskItem MapToEmployeeTaskItem(TaskItem task)
        {
            var now = DateTime.UtcNow;
            return new EmployeeTaskItem
            {
                Id = task.Id,
                Title = task.Title,
                ProjectName = task.Project?.ProjectName ?? "N/A",
                Priority = task.Priority.ToString(),
                PriorityBadgeClass = GetPriorityBadge(task.Priority),
                Status = task.Status.ToString(),
                StatusBadgeClass = GetTaskStatusBadge(task.Status),
                DueDate = task.DueDate,
                IsOverdue = task.DueDate.HasValue && task.DueDate.Value < now && task.Status != ERP.DAL.Models.TaskStatus.Completed,
                IsDueSoon = task.DueDate.HasValue && task.DueDate.Value >= now && task.DueDate.Value <= now.AddDays(3)
            };
        }

        private static string GetPriorityBadge(TaskPriority priority) => priority switch
        {
            TaskPriority.Critical => "bg-danger",
            TaskPriority.High => "bg-warning text-dark",
            TaskPriority.Medium => "bg-info",
            TaskPriority.Low => "bg-secondary",
            _ => "bg-light text-dark"
        };

        private static string GetTaskStatusBadge(ERP.DAL.Models.TaskStatus status) => status switch
        {
            ERP.DAL.Models.TaskStatus.InProgress => "bg-primary",
            ERP.DAL.Models.TaskStatus.Completed => "bg-success",
            ERP.DAL.Models.TaskStatus.Blocked => "bg-danger",
            ERP.DAL.Models.TaskStatus.New => "bg-info",
            _ => "bg-secondary"
        };

        private static string GetProjectStatusBadge(ProjectStatus status) => status switch
        {
            ProjectStatus.InProgress => "bg-primary",
            ProjectStatus.Completed => "bg-success",
            ProjectStatus.OnHold => "bg-warning text-dark",
            _ => "bg-secondary"
        };

        private static string GetTimeAgo(DateTime createdAt)
        {
            var span = DateTime.UtcNow - createdAt;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return createdAt.ToString("MMM dd");
        }
    }
}
