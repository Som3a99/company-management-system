using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ERP.PL.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Full notifications page with filtering
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            NotificationSeverity? severity = null,
            NotificationType? type = null,
            int page = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Forbid();

            var pageSize = 30;
            var notifications = await _notificationService.GetForUserAsync(userId, take: pageSize * page);

            // Apply client-side filters (simple approach, sufficient for typical notification volumes)
            if (severity.HasValue)
                notifications = notifications.Where(n => n.Severity == severity.Value).ToList();
            if (type.HasValue)
                notifications = notifications.Where(n => n.Type == type.Value).ToList();

            // Paginate
            var pagedNotifications = notifications
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentSeverity = severity;
            ViewBag.CurrentType = type;
            ViewBag.CurrentPage = page;
            ViewBag.HasMore = notifications.Count > page * pageSize;

            return View(pagedNotifications);
        }

        /// <summary>
        /// API: Get unread count for the bell badge (called via AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Json(new { count = 0 });

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Json(new { count });
        }

        /// <summary>
        /// API: Get recent notifications for the bell dropdown (called via AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Recent()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Json(Array.Empty<object>());

            var notifications = await _notificationService.GetForUserAsync(userId, take: 10);
            var result = notifications.Select(n => new
            {
                n.Id,
                n.Title,
                Message = n.Message.Length > 80 ? n.Message[..80] + "…" : n.Message,
                n.LinkUrl,
                n.IsRead,
                Severity = n.Severity.ToString().ToLower(),
                TimeAgo = GetTimeAgo(n.CreatedAt),
                n.CreatedAt
            });

            return Json(result);
        }

        /// <summary>
        /// API: Mark a single notification as read
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Forbid();

            await _notificationService.MarkReadAsync(id, userId);
            return Ok();
        }

        /// <summary>
        /// API: Mark all notifications as read
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Forbid();

            await _notificationService.MarkAllReadAsync(userId);
            return Ok();
        }

        private static string GetTimeAgo(DateTime createdAt)
        {
            var span = DateTime.UtcNow - createdAt;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return createdAt.ToString("MMM dd");
        }

        /// <summary>
        /// API: Get current user's notification preferences
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Preferences()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Forbid();

            var prefs = await _notificationService.GetPreferencesAsync(userId);
            return Json(new
            {
                prefs.InAppEnabled,
                prefs.EmailEnabled,
                prefs.MuteTaskAssigned,
                prefs.MuteTaskStatusChanged,
                prefs.MuteReportNotifications
            });
        }

        /// <summary>
        /// API: Update current user's notification preferences
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdatePreferences(
            bool inAppEnabled = true,
            bool emailEnabled = false,
            bool muteTaskAssigned = false,
            bool muteTaskStatusChanged = false,
            bool muteReportNotifications = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Forbid();

            var prefs = new NotificationPreference
            {
                UserId = userId,
                InAppEnabled = inAppEnabled,
                EmailEnabled = emailEnabled,
                MuteTaskAssigned = muteTaskAssigned,
                MuteTaskStatusChanged = muteTaskStatusChanged,
                MuteReportNotifications = muteReportNotifications
            };

            await _notificationService.UpdatePreferencesAsync(prefs);
            return Ok();
        }
    }
}
