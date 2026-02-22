using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ERP.PL.Security
{
    /// <summary>
    /// Custom claims factory to populate user context from Employee entity.
    /// Claims are loaded at login and cached in authentication cookie.
    /// </summary>
    public class ApplicationUserClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cacheService;

        private sealed class ClaimsSnapshot
        {
            public int? EmployeeId { get; init; }
            public int? DepartmentId { get; init; }
            public int? ManagedDepartmentId { get; init; }
            public int? ManagedProjectId { get; init; }
            public int? AssignedProjectId { get; init; }
        }

        public ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options,
            ApplicationDbContext context,
            ICacheService cacheService)
            : base(userManager, roleManager, options)
        {
            _context = context;
            _cacheService=cacheService;
        }

        /// <summary>
        /// Override to add custom claims from Employee entity
        /// </summary>
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            // Get base claims (Id, Email, Roles, etc.)
            var identity = await base.GenerateClaimsAsync(user);

            if (!user.EmployeeId.HasValue)
            {
                return identity;
            }
            var key = $"{CacheKeys.UserClaimsPrefix}{user.Id}:claims";
            var snapshot = await _cacheService.GetOrCreateSafeAsync(
                key,
                async () => await BuildClaimsSnapshotAsync(user.EmployeeId.Value),
                TimeSpan.FromMinutes(5));

            if (snapshot.EmployeeId.HasValue)
                identity.AddClaim(new Claim("EmployeeId", snapshot.EmployeeId.Value.ToString()));

            if (snapshot.DepartmentId.HasValue)
                identity.AddClaim(new Claim("DepartmentId", snapshot.DepartmentId.Value.ToString()));

            if (snapshot.ManagedDepartmentId.HasValue)
                identity.AddClaim(new Claim("ManagedDepartmentId", snapshot.ManagedDepartmentId.Value.ToString()));

            if (snapshot.ManagedProjectId.HasValue)
                identity.AddClaim(new Claim("ManagedProjectId", snapshot.ManagedProjectId.Value.ToString()));

            if (snapshot.AssignedProjectId.HasValue)
                identity.AddClaim(new Claim("AssignedProjectId", snapshot.AssignedProjectId.Value.ToString()));

            return identity;
        }

        private async Task<ClaimsSnapshot> BuildClaimsSnapshotAsync(int employeeId)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.ManagedDepartment)
                .Include(e => e.ManagedProject)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null || employee.IsDeleted)
            {
                return new ClaimsSnapshot();
            }

            return new ClaimsSnapshot
            {
                EmployeeId = employee.Id,
                DepartmentId = employee.DepartmentId,
                ManagedDepartmentId = employee.ManagedDepartment?.Id,
                ManagedProjectId = employee.ManagedProject?.Id,
                AssignedProjectId = employee.ProjectId
            };
        }
    }
}
