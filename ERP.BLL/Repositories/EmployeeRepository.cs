using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Expressions;

namespace ERP.BLL.Repositories
{
    public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public EmployeeRepository(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor) : base(context)
        {
            _httpContextAccessor=httpContextAccessor;
        }

        private IQueryable<Employee> ApplyScopeFilter(IQueryable<Employee> query)
        {
            if (IsCEO())
                return query;

            var managedDeptId = GetManagedDepartmentId();
            if (managedDeptId.HasValue)
                return query.Where(e => e.DepartmentId == managedDeptId.Value);

            var managedProjectId = GetManagedProjectId();
            if (managedProjectId.HasValue)
                return query.Where(e => e.ProjectId == managedProjectId.Value);

            var userDeptId = GetUserDepartmentId();
            if (userDeptId.HasValue)
                return query.Where(e => e.DepartmentId == userDeptId.Value);

            return query.Where(_ => false);
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

        private int? GetManagedProjectId()
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("ManagedProjectId");
            return claim != null ? int.Parse(claim.Value) : null;
        }

        private int? GetUserDepartmentId()
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("DepartmentId");
            return claim != null ? int.Parse(claim.Value) : null;
        }

        // Override GetAll to include Department navigation property
        public override async Task<IEnumerable<Employee>> GetAllAsync()
        {
            var query = _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => !e.IsDeleted);

            // CEO sees all employees
            if (IsCEO())
            {
                return await query.ToListAsync();
            }

            // Department manager sees employees in own department
            var managedDeptId = GetManagedDepartmentId();
            if (managedDeptId.HasValue)
            {
                query = query.Where(e => e.DepartmentId == managedDeptId.Value);
                return await query.ToListAsync();
            }

            // Project manager sees employees on own project
            var managedProjectId = GetManagedProjectId();
            if (managedProjectId.HasValue)
            {
                query = query.Where(e => e.ProjectId == managedProjectId.Value);
                return await query.ToListAsync();
            }

            // Regular employee sees peers in same department
            var userDeptId = GetUserDepartmentId();
            if (userDeptId.HasValue)
            {
                query = query.Where(e => e.DepartmentId == userDeptId.Value);
                return await query.ToListAsync();
            }

            // No context = no employees
            return new List<Employee>();
        }

        // Override GetById to include Department navigation property
        public override async Task<Employee?> GetByIdAsync(int id)
        {
            return await ApplyScopeFilter(_context.Employees
                .AsNoTracking()
                .Include(e => e.Department))
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        /// <summary>
        /// Get employee by ID with tracking enabled (for update operations)
        /// </summary>
        public override async Task<Employee?> GetByIdTrackedAsync(int id)
        {
            return await _context.Employees
                .IgnoreQueryFilters()
                .Include(e => e.Department)
                .Include(e => e.ManagedDepartment)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<IEnumerable<Employee>> GetEmployeesWithoutDepartmentAsync()
        {
            return await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => !e.DepartmentId.HasValue && e.IsActive && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        /// <summary>
        /// Override DeleteAsync for employee-specific business rules
        /// </summary>
        public override async Task DeleteAsync(int id)
        {
            // Call base implementation for standard soft delete
            await base.DeleteAsync(id);

            // NOTE: The controller handles additional business logic like
            // checking if employee is a department manager before calling DeleteAsync
        }

        /// <summary>
        /// Check if email exists (case-insensitive)
        /// </summary>
        public async Task<bool> EmailExistsAsync(string email, int? excludeEmployeeId = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var normalizedEmail = email.Trim().ToLowerInvariant();

            var query = _context.Employees
                .AsNoTracking()
                .Where(e => e.Email.ToLower() == normalizedEmail);

            if (excludeEmployeeId.HasValue)
            {
                query = query.Where(e => e.Id != excludeEmployeeId.Value);
            }

            return await query.AnyAsync();
        }

        /// <summary>
        /// Get employee by email
        /// </summary>
        public async Task<Employee?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            var normalizedEmail = email.Trim().ToLowerInvariant();

            return await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Email.ToLower() == normalizedEmail);
        }

        /// <summary>
        /// Get all employees assigned to a specific project
        /// </summary>
        public async Task<IEnumerable<Employee>> GetEmployeesByProjectIdAsync(int projectId)
        {
            return await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => e.ProjectId == projectId && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        /// <summary>
        /// Get all employees NOT assigned to any project (available for assignment)
        /// </summary>
        public async Task<IEnumerable<Employee>> GetUnassignedEmployeesAsync()
        {
            return await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => !e.ProjectId.HasValue && e.IsActive && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        /// <summary>
        /// Get employee with all related data for profile page
        /// Includes: Department, ManagedDepartment, ManagedProject, Project
        /// </summary>
        public async Task<Employee?> GetEmployeeProfileAsync(int id)
        {
            return await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                    .ThenInclude(d => d!.Manager) // Department's manager
                .Include(e => e.ManagedDepartment) // If this employee manages a department
                    .ThenInclude(d => d!.Employees.Where(emp => !emp.IsDeleted)) // Employees in managed dept
                .Include(e => e.ManagedProject) // If this employee manages a project
                    .ThenInclude(p => p!.Department) // Project's department
                .Include(e => e.Project) // Project this employee is assigned to
                    .ThenInclude(p => p!.Department) // Assigned project's department
                .Include(e => e.Project)
                    .ThenInclude(p => p!.ProjectManager) // Assigned project's manager
                .Where(e => e.Id == id && !e.IsDeleted)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get paginated employees with department info
        /// </summary>
        public override async Task<PagedResult<Employee>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<Employee, bool>>? filter = null,
            Func<IQueryable<Employee>, IOrderedQueryable<Employee>>? orderBy = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IQueryable<Employee> query = ApplyScopeFilter(_context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => !e.IsDeleted));

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
                query = query.OrderBy(e => e.LastName).ThenBy(e => e.FirstName);
            }

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Employee>(items, totalCount, pageNumber, pageSize);
        }
    }
}
