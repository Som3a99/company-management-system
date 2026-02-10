using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.Services;
using ERP.PL.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IAuditService _auditService;
        private readonly ApplicationDbContext _context;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountController> logger,
            IAuditService auditService,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _auditService=auditService;
            _context=context;
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Find user by email
            var user = await _userManager.FindByEmailAsync(model.EmployeeEmail);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, $"No user found with email: {model.EmployeeEmail}");
                return View(model);
            }

            // Remove old password
            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Failed to reset password.");
                return View(model);
            }

            // Add new password
            var addResult = await _userManager.AddPasswordAsync(user, model.TemporaryPassword);
            if (!addResult.Succeeded)
            {
                foreach (var error in addResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            // Force password change on next login
            user.RequirePasswordChange = true;
            await _userManager.UpdateAsync(user);

            // Log the action
            var admin = await _userManager.GetUserAsync(User);
            _logger.LogWarning(
                $"Password reset by admin {admin?.Email} for user {user.Email}. " +
                $"Verification: {model.VerificationNotes ?? "None provided"}");

            TempData["SuccessMessage"] =
                $"Password reset successfully for {user.Email}. " +
                $"User must change password on next login.";

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
        #endregion
    }
}
