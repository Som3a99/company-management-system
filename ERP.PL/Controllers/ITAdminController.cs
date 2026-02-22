using AutoMapper;
using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.Services;
using ERP.PL.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace ERP.PL.Controllers
{
    /// <summary>
    /// IT Admin portal for system administration tasks
    /// Only accessible to CEO and IT Admin
    /// </summary>
    [Authorize(Roles = "CEO,ITAdmin")]
    public class ITAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _auditService;
        private readonly ILogger<ITAdminController> _logger;
        private readonly IMapper _mapper;
        private readonly ICacheService _cacheService;
        private readonly IMemoryCache _memoryCache;

        public ITAdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUnitOfWork unitOfWork,
            IAuditService auditService,
            ILogger<ITAdminController> logger,
            IMapper mapper,
            ICacheService cacheService,
            IMemoryCache memoryCache)
        {
            _context = context;
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _auditService = auditService;
            _logger = logger;
            _mapper = mapper;
            _cacheService=cacheService;
            _memoryCache=memoryCache;
        }

        #region Dashboard

        /// <summary>
        /// IT Admin dashboard with system overview
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var stats = await _cacheService.GetOrCreateSafeAsync(
                CacheKeys.ItAdminDashboard,
                async () => new ITAdminDashboardStats
                {
                    PendingResets = await _context.PasswordResetRequests
                        .CountAsync(r => r.Status == ResetStatus.Pending),
                    ExpiredResets = await _context.PasswordResetRequests
                        .CountAsync(r => r.Status == ResetStatus.Pending && r.ExpiresAt < DateTime.UtcNow),
                    LockedAccounts = await _userManager.Users
                        .CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.Now)
                },
                TimeSpan.FromMinutes(2));

            ViewBag.PendingResets = stats.PendingResets;
            ViewBag.ExpiredResets = stats.ExpiredResets;
            ViewBag.LockedAccounts = stats.LockedAccounts;
            Response.Headers["X-Cache-Context"] = "erp:dashboard:itadmin";

            var memoryStats = (_memoryCache as MemoryCache)?.GetCurrentStatistics();
            var totalLookups = (memoryStats?.TotalHits ?? 0) + (memoryStats?.TotalMisses ?? 0);
            var hitRatio = totalLookups == 0 ? 0 : Math.Round((memoryStats?.TotalHits ?? 0) * 100.0 / totalLookups, 2);

            ViewBag.CacheHitRatio = hitRatio;
            ViewBag.CacheEntryCount = memoryStats?.CurrentEntryCount ?? 0;
            ViewBag.CacheEstimatedSize = memoryStats?.CurrentEstimatedSize ?? 0;

            if ((memoryStats?.CurrentEntryCount ?? 0) > 900)
            {
                ViewBag.CachePressureWarning = "Cache entry count is near configured limit. Review eviction and TTLs.";
            }


            return View();
        }

        #endregion

        #region Password Reset Requests

        /// <summary>
        /// Display all password reset requests with filtering
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PasswordResetRequests(
            int pageNumber = 1,
            int pageSize = 10,
            string? status = null)
        {
            ViewData["StatusFilter"] = status ?? "pending";

            var query = _context.PasswordResetRequests.AsQueryable();

            // Apply status filter
            if (status == "pending")
            {
                query = query.Where(r => r.Status == ResetStatus.Pending && r.ExpiresAt > DateTime.UtcNow);
            }
            else if (status == "expired")
            {
                query = query.Where(r => r.Status == ResetStatus.Pending && r.ExpiresAt <= DateTime.UtcNow);
            }
            else if (status == "approved")
            {
                query = query.Where(r => r.Status == ResetStatus.Approved);
            }
            else if (status == "denied")
            {
                query = query.Where(r => r.Status == ResetStatus.Denied);
            }

            // Order by most recent first
            query = query.OrderByDescending(r => r.RequestedAt);

            var totalCount = await query.CountAsync();
            var requests = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userIds = requests.Select(r => r.UserId).Distinct().ToList();
            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.EmployeeId })
                .ToListAsync();
            var userLookup = users.ToDictionary(u => u.Id, u => u.EmployeeId);

            var employeeIds = users
                .Where(u => u.EmployeeId.HasValue)
                .Select(u => u.EmployeeId!.Value)
                .Distinct()
                .ToList();
            var employees = await _context.Employees
                .Where(e => employeeIds.Contains(e.Id))
                .Select(e => new { e.Id, e.FirstName, e.LastName })
                .ToListAsync();
            var employeeLookup = employees.ToDictionary(e => e.Id, e => $"{e.FirstName} {e.LastName}");

            // Map to view models
            var viewModels = new List<PasswordResetRequestViewModel>();

            foreach (var request in requests)
            {
                userLookup.TryGetValue(request.UserId, out var employeeId);
                var employeeName = employeeId.HasValue && employeeLookup.TryGetValue(employeeId.Value, out var name)
                    ? name
                    : null;

                viewModels.Add(new PasswordResetRequestViewModel
                {
                    Id = request.Id,
                    TicketNumber = request.TicketNumber,
                    UserEmail = request.UserEmail,
                    EmployeeName = employeeName,
                    Status = request.Status,
                    RequestedAt = request.RequestedAt,
                    ExpiresAt = request.ExpiresAt,
                    ResolvedAt = request.ResolvedAt,
                    ResolvedBy = request.ResolvedBy,
                    DenialReason = request.DenialReason,
                    IpAddress = FormatIpAddress(request.IpAddress)
                });
            }

            var pagedResult = new PagedResult<PasswordResetRequestViewModel>(
                viewModels,
                totalCount,
                pageNumber,
                pageSize
            );

            return View(pagedResult);
        }

        /// <summary>
        /// Approve password reset request and set temporary password
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveReset(int requestId, string temporaryPassword)
        {
            if (string.IsNullOrWhiteSpace(temporaryPassword))
            {
                TempData["ErrorMessage"] = "Temporary password is required";
                return RedirectToAction(nameof(PasswordResetRequests));
            }

            var request = await _context.PasswordResetRequests.FindAsync(requestId);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Reset request not found";
                return RedirectToAction(nameof(PasswordResetRequests));
            }

            if (request.Status != ResetStatus.Pending)
            {
                TempData["ErrorMessage"] = $"Request already {request.Status}";
                return RedirectToAction(nameof(PasswordResetRequests));
            }

            if (request.ExpiresAt < DateTime.UtcNow)
            {
                request.Status = ResetStatus.Expired;
                await _context.SaveChangesAsync();
                TempData["ErrorMessage"] = "Request has expired";
                return RedirectToAction(nameof(PasswordResetRequests));
            }

            var user = await _userManager.FindByIdAsync(request.UserId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(PasswordResetRequests));
            }

            try
            {
                // Remove old password
                await _userManager.RemovePasswordAsync(user);

                // Set temporary password
                var result = await _userManager.AddPasswordAsync(user, temporaryPassword);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    TempData["ErrorMessage"] = $"Password validation failed: {errors}";
                    return RedirectToAction(nameof(PasswordResetRequests));
                }

                // Force password change on next login
                user.RequirePasswordChange = true;

                // Reset lockout
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;

                await _userManager.UpdateAsync(user);

                // Update request status
                request.Status = ResetStatus.Approved;
                request.ResolvedAt = DateTime.UtcNow;
                request.ResolvedBy = User.Identity!.Name;
                await _context.SaveChangesAsync();
                await InvalidateDashboardCacheAsync();

                // Audit log
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "PASSWORD_RESET_APPROVED",
                    "PasswordResetRequest",
                    request.Id,
                    details: $"Ticket: {request.TicketNumber}, User: {user.Email}");

                _logger.LogWarning($"Password reset approved by {User.Identity.Name} for {user.Email}");

                TempData["SuccessMessage"] = $"Password reset approved for {user.Email}. Ticket: {request.TicketNumber}";
                TempData["ApprovedTempPassword"] = temporaryPassword;
                TempData["ApprovedTempPasswordTicket"] = request.TicketNumber;
                return RedirectToAction(nameof(PasswordResetRequests));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving password reset for request {requestId}");

                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "PASSWORD_RESET_APPROVE_FAILED",
                    "PasswordResetRequest",
                    requestId,
                    succeeded: false,
                    errorMessage: ex.Message);

                TempData["ErrorMessage"] = "An error occurred while approving the reset request";
                return RedirectToAction(nameof(PasswordResetRequests));
            }
        }

        /// <summary>
        /// Deny password reset request (requires in-person verification)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DenyReset(int requestId, string? denialReason)
        {
            var request = await _context.PasswordResetRequests.FindAsync(requestId);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Reset request not found";
                return RedirectToAction(nameof(PasswordResetRequests));
            }

            if (request.Status != ResetStatus.Pending)
            {
                TempData["ErrorMessage"] = $"Request already {request.Status}";
                return RedirectToAction(nameof(PasswordResetRequests));
            }

            // Update request status
            request.Status = ResetStatus.Denied;
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedBy = User.Identity!.Name;
            request.DenialReason = denialReason ?? "Requires in-person verification";
            await _context.SaveChangesAsync();
            await InvalidateDashboardCacheAsync();

            // Audit log
            await _auditService.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                User.Identity!.Name!,
                "PASSWORD_RESET_DENIED",
                "PasswordResetRequest",
                request.Id,
                details: $"Ticket: {request.TicketNumber}, Reason: {request.DenialReason}");

            TempData["SuccessMessage"] = $"Reset request denied. Ticket: {request.TicketNumber}";
            return RedirectToAction(nameof(PasswordResetRequests));
        }

        /// <summary>
        /// Mark expired requests as expired (cleanup task)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkExpiredRequests()
        {
            var expiredRequests = await _context.PasswordResetRequests
                .Where(r => r.Status == ResetStatus.Pending && r.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var request in expiredRequests)
            {
                request.Status = ResetStatus.Expired;
            }

            await _context.SaveChangesAsync();
            await InvalidateDashboardCacheAsync();

            TempData["SuccessMessage"] = $"Marked {expiredRequests.Count} expired request(s)";
            return RedirectToAction(nameof(PasswordResetRequests));
        }

        #endregion

        #region Helper Method
        private static string? FormatIpAddress(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return null;
            }

            if (ipAddress == "::1")
            {
                return "127.0.0.1 (localhost)";
            }

            if (ipAddress.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
            {
                return ipAddress.Replace("::ffff:", string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            return ipAddress;
        }

        /// <summary>
        /// Invalidation rule: dashboard aggregates depend on password reset requests and lockout state.
        /// </summary>
        private Task InvalidateDashboardCacheAsync() => _cacheService.RemoveAsync(CacheKeys.ItAdminDashboard);
        #endregion

    }
}
