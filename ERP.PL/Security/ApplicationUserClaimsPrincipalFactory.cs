using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ERP.PL.Security
{
    /// <summary>
    /// Custom claims factory to populate user context from Employee entity
    /// Claims are loaded once at login and cached in authentication cookie
    /// </summary>
    public class ApplicationUserClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        private readonly ApplicationDbContext _context;

        public ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options,
            ApplicationDbContext context)
            : base(userManager, roleManager, options)
        {
            _context = context;
        }

        /// <summary>
        /// Override to add custom claims from Employee entity
        /// </summary>
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            // Get base claims (Id, Email, Roles, etc.)
            var identity = await base.GenerateClaimsAsync(user);

            // If user is linked to an employee, add employee context
            if (user.EmployeeId.HasValue)
            {
                // Load employee with related data
                var employee = await _context.Employees
                    .AsNoTracking()
                    .Include(e => e.Department)
                    .Include(e => e.ManagedDepartment)
                    .Include(e => e.ManagedProject)
                    .Include(e => e.Project)
                    .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

                if (employee != null && !employee.IsDeleted)
                {
                    // Add employee ID claim
                    identity.AddClaim(new Claim("EmployeeId", employee.Id.ToString()));

                    // Add department claim
                    if (employee.DepartmentId > 0)
                    {
                        identity.AddClaim(new Claim("DepartmentId", employee.DepartmentId.ToString()));
                    }

                    // Add managed department claim (if manager)
                    if (employee.ManagedDepartment != null)
                    {
                        identity.AddClaim(new Claim("ManagedDepartmentId",
                            employee.ManagedDepartment.Id.ToString()));
                    }

                    // Add managed project claim (if project manager)
                    if (employee.ManagedProject != null)
                    {
                        identity.AddClaim(new Claim("ManagedProjectId",
                            employee.ManagedProject.Id.ToString()));
                    }

                    // Add assigned project claim
                    if (employee.ProjectId.HasValue)
                    {
                        identity.AddClaim(new Claim("AssignedProjectId",
                            employee.ProjectId.Value.ToString()));
                    }
                }
            }

            return identity;
        }
    }
}
