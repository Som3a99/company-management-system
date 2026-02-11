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

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountController> logger,
            IAuditService auditService,
            ApplicationDbContext context,
            DocumentSettings documentSettings)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _auditService=auditService;
            _context=context;
            _documentSettings=documentSettings;
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

                // Redirect to return URL or home
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
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
        /// Display forgot password form
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        /// <summary>
        /// Process forgot password request and create ticket
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
                return View("ForgotPasswordConfirmation", new ForgotPasswordConfirmationViewModel
                {
                    Message = "If this email exists in our system, a password reset request has been submitted for IT review."
                });
            }

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
                ExpiresAt = DateTime.UtcNow.AddHours(1), // 1 hour expiration
                IpAddress = GetClientIpAddress(),
                UserAgent = HttpContext.Request.Headers["User-Agent"].ToString()
            };

            _context.PasswordResetRequests.Add(resetRequest);
            await _context.SaveChangesAsync();

            // Audit log
            await _auditService.LogAsync(
                user.Id,
                user.Email!,
                "PASSWORD_RESET_REQUESTED",
                "PasswordResetRequest",
                resetRequest.Id,
                details: $"Ticket: {ticketNumber}");

            _logger.LogInformation($"Password reset requested for {user.Email}. Ticket: {ticketNumber}");

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
        #endregion
    }
}
