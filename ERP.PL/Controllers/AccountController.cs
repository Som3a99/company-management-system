using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.Helpers;
using ERP.PL.Services;
using ERP.PL.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

namespace ERP.PL.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IAuditService _auditService;
        private readonly ApplicationDbContext _context;
        private static readonly ConcurrentDictionary<string, List<DateTime>> _adminResetAttempts = new();
        private const int AdminResetLimitPerMinute = 5;
        private readonly DocumentSettings _documentSettings;
        private readonly ICacheService _cacheService;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountController> logger,
            IAuditService auditService,
            ApplicationDbContext context,
            DocumentSettings documentSettings,
            ICacheService cacheService,
            INotificationService notificationService,
            IEmailService emailService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _auditService=auditService;
            _context=context;
            _documentSettings=documentSettings;
            _cacheService=cacheService;
            _notificationService = notificationService;
            _emailService = emailService;
        }


        #region Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Find user by email
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                // Log failed login (user not found)
                await _auditService.LogAsync(
                    "UNKNOWN",
                    model.Email,
                    "LOGIN_ATTEMPT",
                    succeeded: false,
                    errorMessage: "User not found");

                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            // Check if account is active
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account has been disabled. Contact IT support.");
                return View(model);
            }

            // Attempt sign-in
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true); // Enable lockout on failed attempts

            if (result.Succeeded)
            {
                // Log successful login
                await _auditService.LogAsync(
                    user.Id,
                    user.Email!,
                    "LOGIN_SUCCESS");
                _logger.LogInformation($"User {user.Email} logged in successfully");

                // Check if password change required
                if (user.RequirePasswordChange)
                {
                    TempData["WarningMessage"] = "You must change your password.";
                    return RedirectToAction(nameof(ChangePassword));
                }

                // Redirect to return URL or role-specific home
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToRoleHome(await _userManager.GetRolesAsync(user));
            }

            if (result.IsLockedOut)
            {
                // Log lockout event
                await _auditService.LogAsync(
                    user.Id,
                    user.Email!,
                    "LOGIN_LOCKOUT",
                    succeeded: false,
                    errorMessage: "Account locked out");
                _logger.LogWarning($"User {user.Email} account locked out");
                ModelState.AddModelError(string.Empty,
                    "Account locked due to multiple failed login attempts. Try again in 15 minutes.");
                return View(model);
            }

            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty,
                    "Email confirmation required. Contact IT support.");
                return View(model);
            }

            // Log failed login (wrong password)
            await _auditService.LogAsync(
                user.Id,
                user.Email!,
                "LOGIN_FAILED",
                succeeded: false,
                errorMessage: "Invalid password");
            // Generic failure message (don't reveal if email exists)
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model); 
        }
        #endregion

        #region Profile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var viewModel = await BuildUserProfileViewModelAsync(user);
            return View(viewModel);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (model.ProfileImage == null)
            {
                TempData["ErrorMessage"] = "Please select an image to upload.";
                return RedirectToAction(nameof(Profile));
            }

            if (!user.EmployeeId.HasValue)
            {
                TempData["ErrorMessage"] = "This account is not linked to an employee record. Contact IT Admin to update your profile picture.";
                return RedirectToAction(nameof(Profile));
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value && !e.IsDeleted);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Employee profile not found.";
                return RedirectToAction(nameof(Profile));
            }

            var oldImageUrl = employee.ImageUrl;

            try
            {
                var uploadedImageUrl = await _documentSettings.UploadImagePath(model.ProfileImage, "images");
                employee.ImageUrl = uploadedImageUrl;

                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(oldImageUrl) && !_documentSettings.IsDefaultAvatar(oldImageUrl))
                {
                    _documentSettings.DeleteImage(oldImageUrl, "images");
                }

                await _auditService.LogAsync(
                    user.Id,
                    user.Email ?? "UNKNOWN",
                    "PROFILE_PICTURE_UPDATED",
                    "Employee",
                    employee.Id,
                    details: "User updated own profile picture");

                TempData["SuccessMessage"] = "Profile picture updated successfully.";
                return RedirectToAction(nameof(Profile));
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update profile picture for user {UserId}", user.Id);

                await _auditService.LogAsync(
                    user.Id,
                    user.Email ?? "UNKNOWN",
                    "PROFILE_PICTURE_UPDATE_FAILED",
                    "Employee",
                    employee.Id,
                    succeeded: false,
                    errorMessage: ex.Message);

                TempData["ErrorMessage"] = "An unexpected error occurred while updating your profile picture.";
                return RedirectToAction(nameof(Profile));
            }
        }
        #endregion

        #region Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out");
            return RedirectToAction("Index", "Home");
        }
        #endregion

        #region Change Password
        [HttpGet]
        [Authorize] // Must be logged in
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var result = await _userManager.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.NewPassword);

            if (result.Succeeded)
            {
                // Clear RequirePasswordChange flag
                if (user.RequirePasswordChange)
                {
                    user.RequirePasswordChange = false;
                    await _userManager.UpdateAsync(user);
                }

                // Refresh sign-in
                await _signInManager.RefreshSignInAsync(user);

                _logger.LogInformation($"User {user.Email} changed password successfully");
                TempData["SuccessMessage"] = "Password changed successfully!";

                return RedirectToAction("Index", "Home");
            }

            // Add errors to model state
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
        #endregion

        #region Reset Password by Admin
        [HttpGet]
        [Authorize(Roles = "CEO,ITAdmin")] // Only CEO and IT Admin
        public IActionResult ResetPasswordByAdmin()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "CEO,ITAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordByAdmin(ResetPasswordByAdminViewModel model)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!await VerifyAdminStepUpAuthenticationAsync(admin, model.AdminCurrentPassword))
            {
                await _auditService.LogAsync(
                    admin.Id,
                    admin.Email ?? "UNKNOWN",
                    "ADMIN_PASSWORD_RESET_STEPUP_FAILED",
                    resourceType: "Account",
                    succeeded: false,
                    errorMessage: "Admin current password validation failed");

                ModelState.AddModelError(string.Empty, "Could not complete password reset request.");
                return View(model);
            }

            if (IsRateLimited(admin.Id))
            {
                await _auditService.LogAsync(
                    admin.Id,
                    admin.Email ?? "UNKNOWN",
                    "ADMIN_PASSWORD_RESET_RATE_LIMITED",
                    resourceType: "Account",
                    succeeded: false,
                    errorMessage: "Rate limit exceeded");

                ModelState.AddModelError(string.Empty, "Too many reset attempts. Please wait a minute and try again.");
                return View(model);
            }

            // Find user by email
            var user = await _userManager.FindByEmailAsync(model.EmployeeEmail);

            if (user == null)
            {
                await _auditService.LogAsync(
                    admin.Id,
                    admin.Email ?? "UNKNOWN",
                    "ADMIN_PASSWORD_RESET_UNKNOWN_TARGET",
                    resourceType: "Account",
                    succeeded: false,
                    errorMessage: "Target account not found",
                    details: JsonSerializer.Serialize(new { model.EmployeeEmail }));

                ModelState.AddModelError(string.Empty, "Could not complete password reset request.");
                return View(model);
            }

            // Remove old password
            if (!await CanResetTargetUserAsync(admin, user))
            {
                await _auditService.LogAsync(
                    admin.Id,
                    admin.Email ?? "UNKNOWN",
                    "ADMIN_PASSWORD_RESET_FORBIDDEN_TARGET",
                    resourceType: "Account",
                    succeeded: false,
                    errorMessage: "Insufficient privileges for target account",
                    details: JsonSerializer.Serialize(new { targetUserId = user.Id, targetEmail = user.Email }));

                ModelState.AddModelError(string.Empty, "You are not allowed to reset this account password.");
                return View(model);
            }

            // Add new password
            // Atomic reset pattern: token-based reset instead of remove/add
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, model.TemporaryPassword);
            if (!resetResult.Succeeded)
            {
                foreach (var error in resetResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                await _auditService.LogAsync(
                        admin.Id,
                        admin.Email ?? "UNKNOWN",
                        "ADMIN_PASSWORD_RESET_FAILED",
                        resourceType: "Account",
                        succeeded: false,
                        errorMessage: string.Join("; ", resetResult.Errors.Select(e => e.Code)),
                        details: JsonSerializer.Serialize(new { targetUserId = user.Id, targetEmail = user.Email }));
                return View(model);
            }

            // Force password change on next login
            user.RequirePasswordChange = true;
            await _userManager.UpdateAsync(user);

            // Log the action
            await _auditService.LogAsync(
                    admin.Id,
                    admin.Email ?? "UNKNOWN",
                    "ADMIN_PASSWORD_RESET_SUCCESS",
                    resourceType: "Account",
                    succeeded: true,
                    details: JsonSerializer.Serialize(new
                    {
                        targetUserId = user.Id,
                        targetEmail = user.Email,
                        verificationNotesProvided = !string.IsNullOrWhiteSpace(model.VerificationNotes)
                    }));
            _logger.LogWarning(
                "Admin password reset completed by {AdminEmail} for target account {TargetEmail}.",
                admin.Email,
                user.Email);

            TempData["SuccessMessage"] =
                $"Password reset successfully for {user.Email}. " +
                "User must change password on next login.";

            return RedirectToAction(nameof(ResetPasswordByAdmin));
        }
        #endregion

        #region Forgot Password

        /// <summary>
        /// Display forgot password form with email access choice
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        /// <summary>
        /// Process forgot password request — self-service email reset or IT ticket
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            // SECURITY: Don't reveal if email exists or not
            if (user == null || !user.IsActive)
            {
                if (model.HasEmailAccess)
                {
                    return View("ForgotPasswordConfirmation", new ForgotPasswordConfirmationViewModel
                    {
                        Message = "If this email exists in our system, a password reset link has been sent to your inbox.",
                        IsEmailReset = true
                    });
                }
                return View("ForgotPasswordConfirmation", new ForgotPasswordConfirmationViewModel
                {
                    Message = "If this email exists in our system, a password reset request has been submitted for IT review."
                });
            }

            // ── PATH A: User HAS email access → send self-service reset link ──
            if (model.HasEmailAccess)
            {
                return await HandleEmailResetAsync(user);
            }

            // ── PATH B: User does NOT have email access → IT Admin ticket flow ──
            return await HandleItTicketResetAsync(user);
        }

        /// <summary>
        /// Self-service email reset: generate token, send link via email
        /// </summary>
        private async Task<IActionResult> HandleEmailResetAsync(ApplicationUser user)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var resetLink = Url.Action(
                "ResetPassword",
                "Account",
                new { userId = user.Id, token },
                protocol: HttpContext.Request.Scheme);

            // Build reset email (HTML)
            var emailBody =
                $"<html><body style='font-family: Inter, -apple-system, sans-serif; color: #334155;'>" +
                $"<div style='max-width: 520px; margin: 0 auto; padding: 32px;'>" +
                $"<div style='background: linear-gradient(135deg, #6366f1, #818cf8); padding: 24px; border-radius: 12px 12px 0 0;'>" +
                $"<h2 style='color: #fff; margin: 0; font-size: 20px;'>Password Reset Request</h2>" +
                $"</div>" +
                $"<div style='background: #f8fafc; padding: 24px; border: 1px solid #e2e8f0; border-top: none; border-radius: 0 0 12px 12px;'>" +
                $"<p>Hello,</p>" +
                $"<p>We received a request to reset your password for <strong>{System.Net.WebUtility.HtmlEncode(user.Email!)}</strong>.</p>" +
                $"<p>Click the button below to set a new password. This link expires in <strong>1 hour</strong>.</p>" +
                $"<div style='text-align: center; margin: 24px 0;'>" +
                $"<a href='{System.Net.WebUtility.HtmlEncode(resetLink!)}' " +
                $"style='background: #6366f1; color: #fff; padding: 12px 32px; border-radius: 8px; text-decoration: none; font-weight: 600; display: inline-block;'>" +
                $"Reset My Password</a></div>" +
                $"<p style='font-size: 13px; color: #64748b;'>If you didn't request this, you can safely ignore this email. " +
                $"Your password will remain unchanged.</p>" +
                $"<hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;'>" +
                $"<p style='font-size: 12px; color: #94a3b8;'>If the button doesn't work, copy and paste this link into your browser:</p>" +
                $"<p style='font-size: 12px; color: #6366f1; word-break: break-all;'>{System.Net.WebUtility.HtmlEncode(resetLink!)}</p>" +
                $"</div></div></body></html>";

            // Fire-and-forget: send email in background so the response returns immediately.
            // SmtpEmailService is a Singleton with its own error handling and timeout protection.
            var userEmail = user.Email!;
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendHtmlAsync(
                        userEmail,
                        "CompanyFlow — Password Reset",
                        emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background: failed to send password reset email to {Email}", userEmail);
                }
            });

            // Audit log
            await _auditService.LogAsync(
                user.Id,
                user.Email!,
                "PASSWORD_RESET_EMAIL_SENT",
                "Account",
                details: "Self-service password reset link sent via email");

            _logger.LogInformation("Password reset email sent to {Email}", user.Email);

            return View("ForgotPasswordConfirmation", new ForgotPasswordConfirmationViewModel
            {
                Message = "A password reset link has been sent to your email address. " +
                         "Please check your inbox (and spam folder) and follow the link to reset your password. " +
                         "The link expires in 1 hour.",
                IsEmailReset = true
            });
        }

        /// <summary>
        /// IT ticket-based reset: create ticket and notify IT Admin (existing flow)
        /// </summary>
        private async Task<IActionResult> HandleItTicketResetAsync(ApplicationUser user)
        {
            // Check if there's already a pending request
            var existingPendingRequest = await _context.PasswordResetRequests
                .Where(r => r.UserId == user.Id && r.Status == ResetStatus.Pending)
                .FirstOrDefaultAsync();

            if (existingPendingRequest != null)
            {
                return View("ForgotPasswordConfirmation", new ForgotPasswordConfirmationViewModel
                {
                    Message = $"A password reset request already exists. Ticket #: {existingPendingRequest.TicketNumber}. " +
                             "Please try logging in after 30 minutes. If still locked, visit the IT desk.",
                    TicketNumber = existingPendingRequest.TicketNumber
                });
            }

            // Generate unique ticket number
            string ticketNumber = await GenerateUniqueTicketNumber();

            // Create password reset request
            var resetRequest = new PasswordResetRequest
            {
                UserId = user.Id,
                UserEmail = user.Email!,
                TicketNumber = ticketNumber,
                Status = ResetStatus.Pending,
                RequestedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IpAddress = GetClientIpAddress(),
                UserAgent = HttpContext.Request.Headers["User-Agent"].ToString()
            };

            _context.PasswordResetRequests.Add(resetRequest);
            await _cacheService.RemoveAsync(CacheKeys.ItAdminDashboard);
            await _context.SaveChangesAsync();

            // Audit log
            await _auditService.LogAsync(
                user.Id,
                user.Email!,
                "PASSWORD_RESET_REQUESTED",
                "PasswordResetRequest",
                resetRequest.Id,
                details: $"Ticket: {ticketNumber}");

            _logger.LogInformation("Password reset requested for {Email}. Ticket: {TicketNumber}", user.Email, ticketNumber);

            // N-10a: Notify IT Admins and CEOs about the password reset request
            try
            {
                var itAdminIds = await GetUserIdsByRoleAsync("ITAdmin");
                var ceoIds = await GetUserIdsByRoleAsync("CEO");
                var recipients = itAdminIds.Union(ceoIds).Distinct();

                await _notificationService.CreateForManyAsync(
                    recipients,
                    title: "Password Reset Request",
                    message: $"Employee {user.Email} has submitted a password reset request. " +
                             $"Ticket: {ticketNumber}. Expires in 1 hour.",
                    type: NotificationType.PasswordResetRequested,
                    severity: NotificationSeverity.Critical,
                    linkUrl: "/ITAdmin/PasswordResetRequests",
                    isSystemGenerated: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send password reset notification for ticket {TicketNumber}", ticketNumber);
            }

            return View("ForgotPasswordConfirmation", new ForgotPasswordConfirmationViewModel
            {
                Message = $"Your password reset request has been submitted for review by IT Admin. " +
                         $"Ticket #: {ticketNumber}. " +
                         "Please try logging in again after 30 minutes. " +
                         "If access is not restored, please visit the IT desk for further verification.",
                TicketNumber = ticketNumber
            });
        }

        #endregion

        #region Self-Service Reset Password (via email token)

        /// <summary>
        /// Display reset password form (reached via email link)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                TempData["ErrorMessage"] = "Invalid password reset link.";
                return RedirectToAction(nameof(Login));
            }

            var model = new ResetPasswordViewModel
            {
                UserId = userId,
                Token = token
            };

            return View(model);
        }

        /// <summary>
        /// Process self-service password reset using token
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                // Don't reveal that user doesn't exist
                TempData["SuccessMessage"] = "Your password has been reset. You can now log in.";
                return RedirectToAction(nameof(Login));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

            if (result.Succeeded)
            {
                // Clear any password change requirement
                if (user.RequirePasswordChange)
                {
                    user.RequirePasswordChange = false;
                    await _userManager.UpdateAsync(user);
                }

                // Reset lockout
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;
                await _userManager.UpdateAsync(user);

                // Audit log
                await _auditService.LogAsync(
                    user.Id,
                    user.Email ?? "UNKNOWN",
                    "PASSWORD_RESET_SELF_SERVICE",
                    "Account",
                    details: "User reset password via email token");

                _logger.LogInformation("User {Email} reset their password via email link", user.Email);

                TempData["SuccessMessage"] = "Your password has been reset successfully. You can now log in with your new password.";
                return RedirectToAction(nameof(Login));
            }

            // Token invalid or expired
            foreach (var error in result.Errors)
            {
                if (error.Code == "InvalidToken")
                {
                    ModelState.AddModelError(string.Empty,
                        "This reset link has expired or has already been used. Please request a new one.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        #endregion

        #region Access Denied
        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Redirects to the appropriate role-specific home dashboard after login.
        /// Priority: CEO > ITAdmin > DepartmentManager > ProjectManager > Employee
        /// </summary>
        private IActionResult RedirectToRoleHome(IList<string> roles)
        {
            if (roles.Contains("CEO"))
                return RedirectToAction("Index", "ExecutiveHome");

            if (roles.Contains("ITAdmin"))
                return RedirectToAction("Index", "ITAdminHome");

            if (roles.Contains("DepartmentManager") || roles.Contains("ProjectManager"))
                return RedirectToAction("Index", "ManagerHome");

            return RedirectToAction("Index", "EmployeeHome");
        }

        /// <summary>
        /// Generate unique ticket number in format RST-YYYY-NNNNNN
        /// </summary>
        private async Task<string> GenerateUniqueTicketNumber()
        {
            string ticketNumber;
            bool exists;

            do
            {
                // Format: RST-2026-001234
                string year = DateTime.UtcNow.Year.ToString();
                string randomPart = Random.Shared.Next(1, 999999).ToString("D6");
                ticketNumber = $"RST-{year}-{randomPart}";

                // Check if ticket number already exists
                exists = await _context.PasswordResetRequests
                    .AnyAsync(r => r.TicketNumber == ticketNumber);

            } while (exists);

            return ticketNumber;
        }

        private string? GetClientIpAddress()
        {
            var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var remoteIp = HttpContext.Connection.RemoteIpAddress;
            if (remoteIp == null)
            {
                return null;
            }

            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            return remoteIp.ToString();
        }

        private async Task<bool> VerifyAdminStepUpAuthenticationAsync(ApplicationUser admin, string currentPassword)
        {
            return await _userManager.CheckPasswordAsync(admin, currentPassword);
        }

        private bool IsRateLimited(string adminUserId)
        {
            var now = DateTime.UtcNow;
            var attempts = _adminResetAttempts.GetOrAdd(adminUserId, _ => new List<DateTime>());

            lock (attempts)
            {
                attempts.RemoveAll(timestamp => timestamp <= now.AddMinutes(-1));

                if (attempts.Count >= AdminResetLimitPerMinute)
                {
                    return true;
                }

                attempts.Add(now);
                return false;
            }
        }

        private async Task<bool> CanResetTargetUserAsync(ApplicationUser admin, ApplicationUser target)
        {
            var adminIsCeo = User.IsInRole("CEO");
            var adminIsItAdmin = User.IsInRole("ITAdmin");

            if (target.Id == admin.Id)
            {
                return false;
            }

            if (adminIsCeo)
            {
                return true;
            }

            if (!adminIsItAdmin)
            {
                return false;
            }

            var targetIsCeo = await _userManager.IsInRoleAsync(target, "CEO");
            var targetIsItAdmin = await _userManager.IsInRoleAsync(target, "ITAdmin");

            // ITAdmin cannot reset CEO or ITAdmin accounts
            return !targetIsCeo && !targetIsItAdmin;
        }

        private async Task<UserProfileViewModel> BuildUserProfileViewModelAsync(ApplicationUser user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            var employee = user.EmployeeId.HasValue
                ? await _context.Employees
                    .AsNoTracking()
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value && !e.IsDeleted)
                : null;

            var employeeRoleFromClaim = User.FindFirstValue(ClaimTypes.Role);

            return new UserProfileViewModel
            {
                UserId = user.Id,
                Username = user.UserName ?? user.Email ?? "N/A",
                Email = user.Email ?? "N/A",
                PhoneNumber = employee?.PhoneNumber ?? user.PhoneNumber,
                AccountCreatedAt = user.CreatedAt,
                AccountStatus = user.IsActive ? "Active" : "Disabled",
                Roles = userRoles.ToList(),
                PrimaryRole = userRoles.FirstOrDefault() ?? employeeRoleFromClaim ?? "Employee",
                EmployeeId = employee?.Id,
                FirstName = employee?.FirstName,
                LastName = employee?.LastName,
                Position = employee?.Position,
                DepartmentName = employee?.Department?.DepartmentName,
                DepartmentCode = employee?.Department?.DepartmentCode,
                HireDate = employee?.HireDate,
                ImageUrl = employee?.ImageUrl ?? "/uploads/images/avatar-user.png"
            };
        }

        private async Task<IList<string>> GetUserIdsByRoleAsync(string roleName)
        {
            var users = await _userManager.GetUsersInRoleAsync(roleName);
            return users
                .Where(u => u.IsActive)
                .Select(u => u.Id)
                .ToList();
        }
        #endregion
    }
}
