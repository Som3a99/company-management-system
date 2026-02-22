using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Services
{
    public class RoleManagementService : IRoleManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RoleManagementService> _logger;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ICacheService _cacheService;

        public RoleManagementService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<RoleManagementService> logger,
            SignInManager<ApplicationUser> signInManager,
            ICacheService cacheService)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _signInManager=signInManager;
            _cacheService=cacheService;
        }

        public async Task SyncEmployeeRolesAsync(int employeeId)
        {
            // Load employee with relationships
            var employee = await _context.Employees
                .Include(e => e.ApplicationUser)
                .Include(e => e.ManagedDepartment)
                .Include(e => e.ManagedProject)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee?.ApplicationUser == null)
            {
                _logger.LogWarning($"Employee {employeeId} has no linked ApplicationUser");
                return;
            }

            var user = employee.ApplicationUser;

            // Ensure base "Employee" role
            if (!await _userManager.IsInRoleAsync(user, "Employee"))
            {
                await _userManager.AddToRoleAsync(user, "Employee");
                _logger.LogInformation($"Added 'Employee' role to {user.Email}");
            }

            // Check if employee manages a department
            if (employee.ManagedDepartment != null)
            {
                if (!await _userManager.IsInRoleAsync(user, "DepartmentManager"))
                {
                    await _userManager.AddToRoleAsync(user, "DepartmentManager");
                    _logger.LogInformation($"Added 'DepartmentManager' role to {user.Email}");
                }
            }
            else if (await _userManager.IsInRoleAsync(user, "DepartmentManager"))
            {
                // Remove DepartmentManager role if no longer managing
                await _userManager.RemoveFromRoleAsync(user, "DepartmentManager");
                _logger.LogInformation($"Removed 'DepartmentManager' role from {user.Email}");
            }

            // Check if employee manages a project
            if (employee.ManagedProject != null)
            {
                if (!await _userManager.IsInRoleAsync(user, "ProjectManager"))
                {
                    await _userManager.AddToRoleAsync(user, "ProjectManager");
                    _logger.LogInformation($"Added 'ProjectManager' role to {user.Email}");
                }
            }
            else if (await _userManager.IsInRoleAsync(user, "ProjectManager"))
            {
                // Remove ProjectManager role if no longer managing
                await _userManager.RemoveFromRoleAsync(user, "ProjectManager");
                _logger.LogInformation($"Removed 'ProjectManager' role from {user.Email}");
            }

            await _cacheService.RemoveAsync(CacheKeys.AvailableProjectManagersAll);
            await _cacheService.RemoveAsync($"{CacheKeys.UserClaimsPrefix}{user.Id}:claims");
            await _signInManager.RefreshSignInAsync(user);
        }

        public async Task RemoveManagementRolesAsync(string applicationUserId)
        {
            var user = await _userManager.FindByIdAsync(applicationUserId);
            if (user == null)
                return;

            if (await _userManager.IsInRoleAsync(user, "DepartmentManager"))
            {
                await _userManager.RemoveFromRoleAsync(user, "DepartmentManager");
            }

            if (await _userManager.IsInRoleAsync(user, "ProjectManager"))
            {
                await _userManager.RemoveFromRoleAsync(user, "ProjectManager");
            }


            await _cacheService.RemoveAsync(CacheKeys.AvailableProjectManagersAll);
            await _cacheService.RemoveAsync($"{CacheKeys.UserClaimsPrefix}{user.Id}:claims");
            await _signInManager.RefreshSignInAsync(user);
        }
    }
}
