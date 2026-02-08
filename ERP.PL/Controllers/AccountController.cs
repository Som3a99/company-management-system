using ERP.DAL.Models;
using ERP.PL.Services;
using ERP.PL.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PL.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IAuditService _auditService;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountController> logger,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _auditService=auditService;
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

        #region Access Denied
        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        } 
        #endregion
    }
}
