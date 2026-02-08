using AutoMapper;
using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.Services;
using ERP.PL.ViewModels.UserManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ERP.PL.Controllers
{

    /// <summary>
    /// Manages user account creation and administration
    /// Only accessible to CEO and IT Admin
    /// </summary>
    [Authorize(Roles = "CEO,ITAdmin")]
    public class UserManagementController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;
        private readonly IRoleManagementService _roleManagementService;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IMapper mapper,
            IAuditService auditService,
            IRoleManagementService roleManagementService,
            ILogger<UserManagementController> logger)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _mapper = mapper;
            _auditService = auditService;
            _roleManagementService = roleManagementService;
            _logger = logger;
        }

        #region Index - All User Accounts

        /// <summary>
        /// Display all user accounts with pagination
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10)
        {
            var allUsers = _userManager.Users.OrderBy(u => u.Email);

            var totalCount = allUsers.Count();
            var users = allUsers
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var viewModels = new List<UserAccountListViewModel>();

            foreach (var user in users)
            {
                var employee = user.EmployeeId.HasValue
                    ? await _unitOfWork.EmployeeRepository.GetByIdAsync(user.EmployeeId.Value)
                    : null;

                var roles = await _userManager.GetRolesAsync(user);

                viewModels.Add(new UserAccountListViewModel
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    EmployeeId = user.EmployeeId,
                    EmployeeName = employee != null
                        ? $"{employee.FirstName} {employee.LastName}"
                        : null,
                    IsActive = user.IsActive,
                    RequirePasswordChange = user.RequirePasswordChange,
                    CreatedAt = user.CreatedAt,
                    Roles = roles.ToList(),
                    AccessFailedCount = user.AccessFailedCount,
                    LockoutEnd = user.LockoutEnd
                });
            }

            var pagedResult = new PagedResult<UserAccountListViewModel>(
                viewModels,
                totalCount,
                pageNumber,
                pageSize
            );

            return View(pagedResult);
        }

        #endregion

        #region Employees Without Accounts

        /// <summary>
        /// Display all employees who don't have user accounts yet
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EmployeesWithoutAccounts(int pageNumber = 1, int pageSize = 10)
        {
            // Get all employees
            var allEmployees = await _unitOfWork.EmployeeRepository.GetAllAsync();

            // Get all user emails (employees with accounts)
            var userEmails = _userManager.Users
                .Where(u => u.EmployeeId.HasValue)
                .Select(u => u.Email!.ToLower())
                .ToHashSet();

            // Filter employees without accounts
            var employeesWithoutAccounts = allEmployees
                .Where(e => !userEmails.Contains(e.Email.ToLower()))
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToList();

            var totalCount = employeesWithoutAccounts.Count;
            var pagedEmployees = employeesWithoutAccounts
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var viewModels = pagedEmployees.Select(e => new EmployeeWithoutAccountViewModel
            {
                EmployeeId = e.Id,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Email = e.Email,
                Position = e.Position,
                DepartmentName = e.Department?.DepartmentName,
                IsDepartmentManager = e.ManagedDepartment != null,
                IsProjectManager = e.ManagedProject != null,
                HireDate = e.HireDate
            }).ToList();

            var pagedResult = new PagedResult<EmployeeWithoutAccountViewModel>(
                viewModels,
                totalCount,
                pageNumber,
                pageSize
            );

            return View(pagedResult);
        }

        #endregion

        #region Create User Account

        /// <summary>
        /// Display form to create user account for an employee
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CreateAccount(int employeeId)
        {
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(employeeId);

            if (employee == null)
                return NotFound();

            // Check if account already exists
            var existingUser = await _userManager.FindByEmailAsync(employee.Email);
            if (existingUser != null)
            {
                TempData["ErrorMessage"] = $"User account already exists for {employee.Email}";
                return RedirectToAction(nameof(EmployeesWithoutAccounts));
            }

            var viewModel = new CreateUserAccountViewModel
            {
                EmployeeId = employee.Id,
                EmployeeName = $"{employee.FirstName} {employee.LastName}",
                EmployeeEmail = employee.Email,
                DepartmentName = employee.Department?.DepartmentName,
                Position = employee.Position,
                IsDepartmentManager = employee.ManagedDepartment != null,
                IsProjectManager = employee.ManagedProject != null
            };

            // Populate available roles
            viewModel.AvailableRoles = GetAvailableRoles();

            // Pre-select Employee role by default
            viewModel.SelectedRoles.Add("Employee");

            // Pre-select management roles if applicable
            if (employee.ManagedDepartment != null)
                viewModel.SelectedRoles.Add("DepartmentManager");
            if (employee.ManagedProject != null)
                viewModel.SelectedRoles.Add("ProjectManager");

            return View(viewModel);
        }

        /// <summary>
        /// Create user account for employee with generated password
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAccount(int employeeId, bool confirmed, List<string> selectedRoles)
        {
            if (!confirmed)
            {
                return RedirectToAction(nameof(CreateAccount), new { employeeId });
            }

            // Validate that at least one role is selected
            if (selectedRoles == null || selectedRoles.Count == 0)
            {
                TempData["ErrorMessage"] = "Please select at least one role for the user account.";
                return RedirectToAction(nameof(CreateAccount), new { employeeId });
            }

            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(employeeId);

            if (employee == null)
                return NotFound();

            // Double-check no account exists
            var existingUser = await _userManager.FindByEmailAsync(employee.Email);
            if (existingUser != null)
            {
                TempData["ErrorMessage"] = $"User account already exists for {employee.Email}";
                return RedirectToAction(nameof(EmployeesWithoutAccounts));
            }

            try
            {
                // Generate secure random password
                string defaultPassword = GenerateSecurePassword();

                // Create user account
                var user = new ApplicationUser
                {
                    UserName = employee.Email,
                    Email = employee.Email,
                    EmailConfirmed = true, // Skip email confirmation
                    EmployeeId = employeeId,
                    RequirePasswordChange = true, // FORCE password change on first login
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user, defaultPassword);

                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));

                    await _auditService.LogAsync(
                        User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                        User.Identity!.Name!,
                        "CREATE_USER_ACCOUNT_FAILED",
                        "ApplicationUser",
                        null,
                        succeeded: false,
                        errorMessage: errors,
                        details: $"Employee: {employee.FirstName} {employee.LastName}");

                    TempData["ErrorMessage"] = $"Failed to create account: {errors}";
                    return RedirectToAction(nameof(CreateAccount), new { employeeId });
                }

                // Assign base "Employee" role
                await _userManager.AddToRoleAsync(user, "Employee");

                // Auto-sync management roles if applicable
                await _roleManagementService.SyncEmployeeRolesAsync(employeeId);

                // Assign selected roles
                foreach (var role in selectedRoles)
                {
                    if (!await _userManager.IsInRoleAsync(user, role))
                    {
                        await _userManager.AddToRoleAsync(user, role);
                    }
                }

                // Update employee record with ApplicationUserId
                var employeeToUpdate = await _unitOfWork.EmployeeRepository.GetByIdTrackedAsync(employeeId);
                if (employeeToUpdate != null)
                {
                    employeeToUpdate.ApplicationUserId = user.Id;
                    _unitOfWork.EmployeeRepository.Update(employeeToUpdate);
                    await _unitOfWork.CompleteAsync();
                }

                // Audit log
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "CREATE_USER_ACCOUNT_SUCCESS",
                    "ApplicationUser",
                    employeeId,
                    details: $"Created account for: {employee.FirstName} {employee.LastName} ({employee.Email})");

                _logger.LogInformation($"User account created for employee {employeeId} by {User.Identity.Name}");

                // Store password in TempData for one-time display
                TempData["GeneratedPassword"] = defaultPassword;
                TempData["NewUserEmail"] = employee.Email;
                TempData["SuccessMessage"] = $"User account created successfully for {employee.FirstName} {employee.LastName}";

                return RedirectToAction(nameof(AccountCreated), new { employeeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating user account for employee {employeeId}");

                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "CREATE_USER_ACCOUNT_FAILED",
                    "ApplicationUser",
                    employeeId,
                    succeeded: false,
                    errorMessage: ex.Message);

                TempData["ErrorMessage"] = "An unexpected error occurred while creating the user account.";
                return RedirectToAction(nameof(CreateAccount), new { employeeId });
            }
        }

        #endregion

        #region Account Created Confirmation

        /// <summary>
        /// Display success page with generated password (ONE-TIME only)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AccountCreated(int employeeId)
        {
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(employeeId);

            if (employee == null)
                return NotFound();

            // Retrieve one-time password from TempData
            var generatedPassword = TempData["GeneratedPassword"] as string;

            if (string.IsNullOrEmpty(generatedPassword))
            {
                TempData["WarningMessage"] = "Password was already displayed. For security, it cannot be shown again.";
            }

            var viewModel = new CreateUserAccountViewModel
            {
                EmployeeId = employee.Id,
                EmployeeName = $"{employee.FirstName} {employee.LastName}",
                EmployeeEmail = employee.Email,
                DepartmentName = employee.Department?.DepartmentName,
                Position = employee.Position,
                GeneratedPassword = generatedPassword,
                IsDepartmentManager = employee.ManagedDepartment != null,
                IsProjectManager = employee.ManagedProject != null
            };

            return View(viewModel);
        }

        #endregion

        #region Toggle Account Status

        /// <summary>
        /// Activate or deactivate user account
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAccountStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            await _auditService.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                User.Identity!.Name!,
                user.IsActive ? "ACCOUNT_ACTIVATED" : "ACCOUNT_DEACTIVATED",
                "ApplicationUser",
                user.EmployeeId,
                details: $"User: {user.Email}");

            TempData["SuccessMessage"] = $"Account {(user.IsActive ? "activated" : "deactivated")} for {user.Email}";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generate cryptographically secure random password
        /// Meets all password policy requirements
        /// </summary>
        private string GenerateSecurePassword()
        {
            const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // No I, O
            const string lowercase = "abcdefghijkmnpqrstuvwxyz"; // No l, o
            const string digits = "23456789"; // No 0, 1
            const string special = "!@#$%^&*";

            var random = RandomNumberGenerator.Create();
            var password = new char[16]; // 16 characters for extra security

            // Ensure at least one of each required type
            password[0] = uppercase[GetRandomIndex(random, uppercase.Length)];
            password[1] = lowercase[GetRandomIndex(random, lowercase.Length)];
            password[2] = digits[GetRandomIndex(random, digits.Length)];
            password[3] = special[GetRandomIndex(random, special.Length)];

            // Fill remaining with random mix
            string allChars = uppercase + lowercase + digits + special;
            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allChars[GetRandomIndex(random, allChars.Length)];
            }

            // Shuffle array
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = GetRandomIndex(random, i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }

        private int GetRandomIndex(RandomNumberGenerator rng, int max)
        {
            byte[] randomBytes = new byte[4];
            rng.GetBytes(randomBytes);
            uint randomValue = BitConverter.ToUInt32(randomBytes, 0);
            return (int)(randomValue % (uint)max);
        }

        // Add helper method for roles
        private List<SelectListItem> GetAvailableRoles()
        {
            var roles = new List<SelectListItem>
            {
                new SelectListItem { Value = "Employee", Text = "Employee" },
                new SelectListItem { Value = "DepartmentManager", Text = "Department Manager" },
                new SelectListItem { Value = "ProjectManager", Text = "Project Manager" },
            };

            // Only show ITAdmin and CEO roles to users with appropriate permissions
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser != null)
            {
                if (_userManager.IsInRoleAsync(currentUser, "ITAdmin").Result ||
                    _userManager.IsInRoleAsync(currentUser, "CEO").Result)
                {
                    roles.Add(new SelectListItem { Value = "ITAdmin", Text = "IT Admin" });
                }

                if (_userManager.IsInRoleAsync(currentUser, "CEO").Result)
                {
                    roles.Add(new SelectListItem { Value = "CEO", Text = "CEO" });
                }
            }

            return roles;
        }

        #endregion
    }
}
