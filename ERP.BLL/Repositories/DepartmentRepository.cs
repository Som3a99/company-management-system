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
        public DepartmentRepository(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor) : base(context)
        {
            _httpContextAccessor=httpContextAccessor;
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
            var query = _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Manager)
                .Include(d => d.Projects)
                .Where(d => !d.IsDeleted);
            // CEO sees all departments
            if (IsCEO())
            {
                return await query.ToListAsync();
            }

            // Department manager sees own department
            var managedDeptId = GetManagedDepartmentId();
            if (managedDeptId.HasValue)
            {
                query = query.Where(d => d.Id == managedDeptId.Value);
                return await query.ToListAsync();
            }

            // Regular employee sees own department
            var userDeptId = GetUserDepartmentId();
            if (userDeptId.HasValue)
            {
                query = query.Where(d => d.Id == userDeptId.Value);
                return await query.ToListAsync();
            }

            // No context = no departments
            return new List<Department>();
        }

        // Override GetById to include Employees navigation property
        public override async Task<Department?> GetByIdAsync(int id)
        {
            return await _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Manager)
                .Include(d => d.Projects)
                .FirstOrDefaultAsync(d => d.Id == id);
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
            return await _context.Departments
                .AsNoTracking()
                .Include(d => d.Manager) // Department manager
                    .ThenInclude(m => m!.Department) // Manager's department (if different)
                .Include(d => d.Employees.Where(e => !e.IsDeleted)) // Active employees
                    .ThenInclude(e => e.Project) // Each employee's project
                .Include(d => d.Projects.Where(p => !p.IsDeleted)) // Active projects
                    .ThenInclude(p => p.ProjectManager) // Each project's manager
                .Include(d => d.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Employees.Where(e => !e.IsDeleted)) // Employees in each project
                .Where(d => d.Id == id && !d.IsDeleted)
                .FirstOrDefaultAsync();
        }

    }
}
