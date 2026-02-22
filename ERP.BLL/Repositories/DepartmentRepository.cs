using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace ERP.BLL.Repositories
{
    public class DepartmentRepository : GenericRepository<Department>, IDepartmentRepository
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICacheService _cacheService;
        public DepartmentRepository(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ICacheService cacheService) : base(context)
        {
            _httpContextAccessor=httpContextAccessor;
            _cacheService=cacheService;
        }

        private bool IsCEO()
        {
            return _httpContextAccessor.HttpContext?.User.IsInRole("CEO") ?? false;
        }

        private int? GetManagedDepartmentId()
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("ManagedDepartmentId");
            return claim != null ? int.Parse(claim.Value) : null;
        }

        private int? GetUserDepartmentId()
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("DepartmentId");
            return claim != null ? int.Parse(claim.Value) : null;
        }

        // Override GetAll to include Employees navigation property
        public override async Task<IEnumerable<Department>> GetAllAsync()
        {
            var cacheKey = BuildScopedDepartmentListKey();
            return await _cacheService.GetOrCreateSafeAsync(
                cacheKey,
                async () =>
                {
                    var query = _context.Departments
                        .AsNoTracking()
                        .Include(d => d.Employees)
                        .Include(d => d.Manager)
                        .Include(d => d.Projects)
                        .Where(d => !d.IsDeleted);

                    if (IsCEO())
                    {
                        return await query.ToListAsync();
                    }

                    var managedDeptId = GetManagedDepartmentId();
                    if (managedDeptId.HasValue)
                    {
                        query = query.Where(d => d.Id == managedDeptId.Value);
                        return await query.ToListAsync();
                    }

                    var userDeptId = GetUserDepartmentId();
                    if (userDeptId.HasValue)
                    {
                        query = query.Where(d => d.Id == userDeptId.Value);
                        return await query.ToListAsync();
                    }

                    return new List<Department>();
                },
                TimeSpan.FromMinutes(10));
        }

        // Override GetById to include Employees navigation property
        public override async Task<Department?> GetByIdAsync(int id)
        {
            var key = $"erp:dept:{id}";
            return await _cacheService.GetOrCreateNullableAsync(
                key,
                async () => await _context.Departments
                    .AsNoTracking()
                    .Include(d => d.Employees)
                    .Include(d => d.Manager)
                    .Include(d => d.Projects)
                    .FirstOrDefaultAsync(d => d.Id == id),
                TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Get department by ID with tracking enabled (for update operations)
        /// </summary>
        public override async Task<Department?> GetByIdTrackedAsync(int id)
        {
            return await _context.Departments
                .IgnoreQueryFilters()
                .Include(d => d.Employees)
                .Include(d => d.Manager)
                .Include(d => d.Projects)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        /// <summary>
        /// Gets a department by its manager ID, optionally excluding a specific department by ID
        ///  Exclude soft-deleted departments
        /// </summary>
        public async Task<Department?> GetByManagerIdAsync(int managerId, int? excludeDepartmentId = null)
        {
            var query = _context.Departments
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .Where(d => d.ManagerId == managerId); // Filter soft-deleted

            if (excludeDepartmentId.HasValue)
                query = query.Where(d => d.Id != excludeDepartmentId.Value);

            return await query.FirstOrDefaultAsync();
        }

        /// <summary>
        /// Check if department code exists (case-insensitive)
        /// </summary>
        public async Task<bool> DepartmentCodeExistsAsync(string code, int? excludeDepartmentId = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            var normalizedCode = code.Trim().ToUpperInvariant();

            var query = _context.Departments
                .AsNoTracking()
                .Where(d => d.DepartmentCode.ToUpper() == normalizedCode);

            if (excludeDepartmentId.HasValue)
            {
                query = query.Where(d => d.Id != excludeDepartmentId.Value);
            }

            return await query.AnyAsync();
        }

        /// <summary>
        /// Get department by code
        /// </summary>
        public async Task<Department?> GetByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var normalizedCode = code.Trim().ToUpperInvariant();

            return await _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Manager)
                .FirstOrDefaultAsync(d => d.DepartmentCode.ToUpper() == normalizedCode);
        }

        /// <summary>
        /// Get all projects for a specific department
        /// </summary>
        public async Task<IEnumerable<Project>> GetProjectsByDepartmentAsync(int departmentId)
        {
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.ProjectManager)
                .Where(p => p.DepartmentId == departmentId && !p.IsDeleted)
                .OrderBy(p => p.ProjectCode)
                .ToListAsync();
        }

        /// <summary>
        /// Get paginated departments with employees and manager
        /// </summary>
        public override async Task<PagedResult<Department>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<Department, bool>>? filter = null,
            Func<IQueryable<Department>, IOrderedQueryable<Department>>? orderBy = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IQueryable<Department> query = _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Manager)
                .Include(d => d.Projects);

            if (filter != null)
            {
                query = query.Where(filter);
            }

            var totalCount = await query.CountAsync();

            if (orderBy != null)
            {
                query = orderBy(query);
            }
            else
            {
                // Default sorting
                query = query.OrderBy(d => d.DepartmentCode);
            }

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Department>(items, totalCount, pageNumber, pageSize);
        }

        /// <summary>
        /// Retrieves the first non-deleted department managed by the specified manager, excluding a given department,
        /// with an update lock for concurrency.
        /// </summary>
        public async Task<Department?> GetDepartmentByManagerForUpdateAsync(int managerId, int? excludeDepartmentId)
        {
            if (excludeDepartmentId.HasValue)
            {
                return await _context.Departments
                    .FromSqlRaw(
                        @"SELECT * FROM Departments WITH (UPDLOCK)
                  WHERE ManagerId = {0}
                  AND Id != {1}
                  AND IsDeleted = 0",
                        managerId,
                        excludeDepartmentId.Value)
                    .AsTracking()
                    .FirstOrDefaultAsync();
            }
            else
            {
                return await _context.Departments
                    .FromSqlRaw(
                        @"SELECT * FROM Departments WITH (UPDLOCK)
                  WHERE ManagerId = {0}
                  AND IsDeleted = 0",
                        managerId)
                    .AsTracking()
                    .FirstOrDefaultAsync();
            }
        }

        /// <summary>
        /// Has active employees in department
        /// </summary>
        /// <param name="departmentId"></param>
        /// <returns></returns>
        public async Task<bool> HasActiveEmployeesAsync(int departmentId)
        {
            return await _context.Employees
                .IgnoreQueryFilters()
                .AnyAsync(e => e.DepartmentId == departmentId && !e.IsDeleted);
        }

        /// <summary>
        /// Get department with all related data for profile page
        /// Includes: Manager, Employees, Projects (with their managers)
        /// </summary>
        public async Task<Department?> GetDepartmentProfileAsync(int id)
        {
            var key = $"erp:dept:{id}:profile";
            return await _cacheService.GetOrCreateNullableAsync(
                key,
                async () => await _context.Departments
                    .AsNoTracking()
                    .Include(d => d.Manager)
                        .ThenInclude(m => m!.Department)
                    .Include(d => d.Employees.Where(e => !e.IsDeleted))
                        .ThenInclude(e => e.Project)
                    .Include(d => d.Projects.Where(p => !p.IsDeleted))
                        .ThenInclude(p => p.ProjectManager)
                    .Include(d => d.Projects.Where(p => !p.IsDeleted))
                        .ThenInclude(p => p.Employees.Where(e => !e.IsDeleted))
                    .Where(d => d.Id == id && !d.IsDeleted)
                    .FirstOrDefaultAsync(),
                TimeSpan.FromMinutes(5));
        }

        public async Task InvalidateCacheAsync(int? departmentId = null)
        {
            await _cacheService.RemoveByPrefixAsync("erp:dept:scope:");
            await _cacheService.RemoveAsync(CacheKeys.DepartmentsAll);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.ReportDepartmentsPrefix);

            if (departmentId.HasValue)
            {
                await _cacheService.RemoveAsync($"erp:dept:{departmentId.Value}");
                await _cacheService.RemoveAsync($"erp:dept:{departmentId.Value}:profile");
            }

            // Department writes can impact manager/dropdown and project-department reports.
            await _cacheService.RemoveAsync(CacheKeys.AvailableProjectManagersAll);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.ReportProjectsPrefix);
        }

        private string BuildScopedDepartmentListKey()
        {
            if (IsCEO())
            {
                return "erp:dept:scope:ceo";
            }

            var managedDeptId = GetManagedDepartmentId();
            if (managedDeptId.HasValue)
            {
                return $"erp:dept:scope:managed:{managedDeptId.Value}";
            }

            var userDeptId = GetUserDepartmentId();
            if (userDeptId.HasValue)
            {
                return $"erp:dept:scope:user:{userDeptId.Value}";
            }

            return "erp:dept:scope:none";
        }

    }
}
