using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.PL.Services
{
    /// <summary>
    /// Background job that checks for tasks due within 48 hours (N-03) and overdue tasks (N-04).
    /// Runs every 6 hours. Avoids duplicate notifications by tracking via audit pattern.
    /// </summary>
    public sealed class TaskDeadlineNotificationJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TaskDeadlineNotificationJob> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

        public TaskDeadlineNotificationJob(
            IServiceScopeFactory scopeFactory,
            ILogger<TaskDeadlineNotificationJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait a bit after startup to let the app fully initialize
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDeadlineNotificationsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex, "TaskDeadlineNotificationJob failed");
                }

                try
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessDeadlineNotificationsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTime.UtcNow;
            var soon = now.AddHours(48);

            // Get active tasks with deadlines that haven't been notified yet
            var activeTasks = await db.TaskItems
                .Include(t => t.AssignedToEmployee)
                    .ThenInclude(e => e!.Department)
                .Include(t => t.Project)
                .Where(t => t.Status != TaskStatus.Completed
                         && t.Status != TaskStatus.Cancelled
                         && t.DueDate.HasValue
                         && t.AssignedToEmployeeId.HasValue
                         && (!t.AlreadyNotifiedDueSoon || !t.AlreadyNotifiedOverdue))
                .ToListAsync(ct);

            int dueSoonCount = 0, overdueCount = 0;

            foreach (var task in activeTasks)
            {
                var assigneeUserId = task.AssignedToEmployee?.ApplicationUserId;
                if (string.IsNullOrEmpty(assigneeUserId))
                    continue;

                if (task.DueDate!.Value < now && !task.AlreadyNotifiedOverdue)
                {
                    // N-04: Task Overdue — notify assignee, project manager, department manager
                    var recipients = new HashSet<string> { assigneeUserId };

                    // Project manager
                    if (task.Project?.ProjectManagerId != null)
                    {
                        var pmUserId = await db.Employees
                            .Where(e => e.Id == task.Project.ProjectManagerId && e.ApplicationUserId != null)
                            .Select(e => e.ApplicationUserId)
                            .FirstOrDefaultAsync(ct);
                        if (pmUserId != null) recipients.Add(pmUserId);
                    }

                    // Department manager (via assignee's department)
                    var deptManagerId = task.AssignedToEmployee?.Department?.ManagerId;
                    if (deptManagerId != null)
                    {
                        var dmUserId = await db.Employees
                            .Where(e => e.Id == deptManagerId.Value && e.ApplicationUserId != null)
                            .Select(e => e.ApplicationUserId)
                            .FirstOrDefaultAsync(ct);
                        if (dmUserId != null) recipients.Add(dmUserId);
                    }

                    await notificationService.CreateForManyAsync(
                        recipients,
                        title: "Task Overdue",
                        message: BuildOverdueMessage(task),
                        type: NotificationType.TaskOverdue,
                        severity: NotificationSeverity.Warning,
                        linkUrl: "/TaskBoard",
                        isSystemGenerated: true);

                    task.AlreadyNotifiedOverdue = true;
                    overdueCount++;
                }
                else if (task.DueDate!.Value <= soon && !task.AlreadyNotifiedDueSoon)
                {
                    // N-03: Task Due Soon — notify assignee only
                    await notificationService.CreateAsync(
                        assigneeUserId,
                        title: "Due in 48 Hours",
                        message: BuildDueSoonMessage(task),
                        type: NotificationType.TaskDueSoon,
                        severity: NotificationSeverity.Warning,
                        linkUrl: "/TaskBoard",
                        isSystemGenerated: true);

                    task.AlreadyNotifiedDueSoon = true;
                    dueSoonCount++;
                }
            }

            // Persist the dedup flags in one batch
            if (dueSoonCount > 0 || overdueCount > 0)
            {
                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "TaskDeadlineNotificationJob: {DueSoon} due-soon, {Overdue} overdue notifications sent.",
                    dueSoonCount, overdueCount);
            }
        }

        private static string BuildDueSoonMessage(TaskItem task)
            => $"Task \"{task.Title}\" is due on {task.DueDate!.Value:MMM dd, yyyy HH:mm} UTC.";

        private static string BuildOverdueMessage(TaskItem task)
            => $"Task \"{task.Title}\" was due on {task.DueDate!.Value:MMM dd, yyyy HH:mm} UTC and is now overdue.";
    }
}
