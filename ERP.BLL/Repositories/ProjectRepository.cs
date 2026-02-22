using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
namespace ERP.BLL.Repositories
{
    public class ProjectRepository : GenericRepository<Project>, IProjectRepository
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICacheService _cacheService;
        public ProjectRepository(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ICacheService cacheService) : base(context)
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

        private int? GetAssignedProjectId()
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("AssignedProjectId");
            return claim != null ? int.Parse(claim.Value) : null;
        }

        private IQueryable<Project> ApplyScopeFilter(IQueryable<Project> query)
        {
            if (IsCEO())
            {
                return query.Where(p => !p.IsDeleted);
            }

            var managedDepartmentId = GetManagedDepartmentId();
            if (managedDepartmentId.HasValue)
            {
                return query.Where(p => p.DepartmentId == managedDepartmentId.Value && !p.IsDeleted);
            }

            var managedProjectId = GetManagedProjectId();
            if (managedProjectId.HasValue)
            {
                return query.Where(p => p.Id == managedProjectId.Value && !p.IsDeleted);
            }

            var userDepartmentId = GetUserDepartmentId();
            if (userDepartmentId.HasValue)
            {
                return query.Where(p => p.DepartmentId == userDepartmentId.Value && !p.IsDeleted);
            }

            var assignedProjectId = GetAssignedProjectId();
            if (assignedProjectId.HasValue)
            {
                return query.Where(p => p.Id == assignedProjectId.Value && !p.IsDeleted);
            }

            return query.Where(p => false); // No access
        }

        // Override GetAll to include navigation properties
        public override async Task<IEnumerable<Project>> GetAllAsync()
        {
            var key = BuildScopedProjectListKey();
            return await _cacheService.GetOrCreateSafeAsync(
                key,
                async () =>
                {
                    var query = _context.Projects
                        .AsNoTracking()
                        .Include(p => p.Department)
                        .Include(p => p.ProjectManager)
                        .Include(p => p.Employees)
                        .Where(p => !p.IsDeleted);

                    if (IsCEO())
                    {
                        return await query.ToListAsync();
                    }

                    var managedDepartmentId = GetManagedDepartmentId();
                    if (managedDepartmentId.HasValue)
                    {
                        query = query.Where(p => p.DepartmentId == managedDepartmentId.Value);
                        return await query.ToListAsync();
                    }

                    var managedProjectId = GetManagedProjectId();
                    if (managedProjectId.HasValue)
                    {
                        query = query.Where(p => p.Id == managedProjectId.Value);
                        return await query.ToListAsync();
                    }

                    var userDepartmentId = GetUserDepartmentId();
                    if (userDepartmentId.HasValue)
                    {
                        query = query.Where(p => p.DepartmentId == userDepartmentId.Value);
                        return await query.ToListAsync();
                    }

                    var assignedProjectId = GetAssignedProjectId();
                    if (assignedProjectId.HasValue)
                    {
                        query = query.Where(p => p.Id == assignedProjectId.Value);
                        return await query.ToListAsync();
                    }

                    return new List<Project>();
                },
                TimeSpan.FromMinutes(10));
        }

        // Override GetById to include navigation properties
        public override async Task<Project?> GetByIdAsync(int id)
        {
            var key = $"erp:proj:{id}";
            return await _cacheService.GetOrCreateNullableAsync(
                key,
                async () => await ApplyScopeFilter(_context.Projects
                    .AsNoTracking()
                    .Include(p => p.Department)
                    .Include(p => p.ProjectManager)
                    .Include(p => p.Employees))
                    .FirstOrDefaultAsync(p => p.Id == id),
                TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Get project by ID with tracking enabled (for update operations)
        /// </summary>
        public override async Task<Project?> GetByIdTrackedAsync(int id)
        {
            return await _context.Projects
                .IgnoreQueryFilters()
                .Include(p => p.Department)
                .Include(p => p.ProjectManager)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        /// <summary>
        /// Check if project code exists (case-insensitive)
        /// </summary>
        public async Task<bool> ProjectCodeExistsAsync(string code, int? excludeProjectId = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            var normalizedCode = code.Trim().ToUpperInvariant();

            var query = _context.Projects
                .AsNoTracking()
                .Where(p => p.ProjectCode.ToUpper() == normalizedCode);

            if (excludeProjectId.HasValue)
            {
                query = query.Where(p => p.Id != excludeProjectId.Value);
            }

            return await query.AnyAsync();
        }

        /// <summary>
        /// Get project by code
        /// </summary>
        public async Task<Project?> GetByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var normalizedCode = code.Trim().ToUpperInvariant();

            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.ProjectManager)
                .FirstOrDefaultAsync(p => p.ProjectCode.ToUpper() == normalizedCode);
        }

        /// <summary>
        /// Get project by manager ID for update operation with locking
        /// </summary>
        public async Task<Project?> GetProjectByManagerForUpdateAsync(int managerId, int? excludeProjectId = null)
        {
            if (excludeProjectId.HasValue)
            {
                return await _context.Projects
                    .FromSqlRaw(
                        @"SELECT * FROM Projects WITH (UPDLOCK)
                          WHERE ProjectManagerId = {0}
                          AND Id != {1}
                          AND IsDeleted = 0",
                        managerId,
                        excludeProjectId.Value)
                    .AsTracking()
                    .FirstOrDefaultAsync();
            }
            else
            {
                return await _context.Projects
                    .FromSqlRaw(
                        @"SELECT * FROM Projects WITH (UPDLOCK)
                          WHERE ProjectManagerId = {0}
                          AND IsDeleted = 0",
                        managerId)
                    .AsTracking()
                    .FirstOrDefaultAsync();
            }
        }

        /// <summary>
        /// Check if employee is already managing a project
        /// </summary>
        public async Task<bool> IsEmployeeManagingProjectAsync(int employeeId, int? excludeProjectId = null)
        {
            var query = _context.Projects
                .AsNoTracking()
                .Where(p => p.ProjectManagerId == employeeId && !p.IsDeleted);

            if (excludeProjectId.HasValue)
            {
                query = query.Where(p => p.Id != excludeProjectId.Value);
            }

            return await query.AnyAsync();
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
        /// Get projects by status
        /// </summary>
        public async Task<IEnumerable<Project>> GetProjectsByStatusAsync(ProjectStatus status)
        {
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.ProjectManager)
                .Where(p => p.Status == status && !p.IsDeleted)
                .OrderBy(p => p.StartDate)
                .ToListAsync();
        }
        /// <summary>
        /// Get all employees assigned to a specific project
        /// </summary>
        public async Task<IEnumerable<Employee>> GetEmployeesByProjectAsync(int projectId)
        {
            return await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => _context.ProjectEmployees.Any(pe => pe.ProjectId == projectId && pe.EmployeeId == e.Id) && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        /// <summary>
        /// Check if employee is already assigned to a project
        /// </summary>
        public async Task<bool> IsEmployeeAssignedToProjectAsync(int employeeId, int? excludeProjectId = null)
        {
            var query = _context.ProjectEmployees
                .AsNoTracking()
                .Where(pe => pe.EmployeeId == employeeId);

            if (excludeProjectId.HasValue)
            {
                query = query.Where(pe => pe.ProjectId != excludeProjectId.Value);
            }

            return await query.AnyAsync();
        }

        /// <summary>
        /// Get project with all employees included
        /// </summary>
        public async Task<Project?> GetProjectWithEmployeesAsync(int projectId)
        {
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.ProjectManager)
                .Include(p => p.Department)
                .ThenInclude(d => d.Employees.Where(e => _context.ProjectEmployees.Any(pe => pe.ProjectId == projectId && pe.EmployeeId == e.Id) && !e.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
        }

        /// <summary>
        /// Get project with all related data for profile page
        /// Includes: Department, ProjectManager, Employees (with Department)
        /// </summary>
        public async Task<Project?> GetProjectProfileAsync(int id)
        {
            var key = $"erp:proj:{id}:profile";
            return await _cacheService.GetOrCreateNullableAsync(
                key,
                async () => await _context.Projects
                    .AsNoTracking()
                    .Include(p => p.Department)
                    .Include(p => p.ProjectManager)
                        .ThenInclude(m => m!.Department)
                    .Include(p => p.Employees.Where(e => !e.IsDeleted))
                        .ThenInclude(e => e.Department)
                    .Include(p => p.ProjectEmployees)
                        .ThenInclude(pe => pe.Employee)
                            .ThenInclude(e => e.Department)
                    .Where(p => p.Id == id && !p.IsDeleted)
                    .FirstOrDefaultAsync(),
                TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Get employees assigned to a specific project as IQueryable
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public async Task<IQueryable<Employee>> GetEmployeesByProjectQueryableAsync(int projectId)
        {
            return await Task.FromResult(_context.Employees
                 .Where(e => _context.ProjectEmployees.Any(pe => pe.ProjectId == projectId && pe.EmployeeId == e.Id) && !e.IsDeleted)
                .Include(e => e.Department)
                .AsQueryable());
        }
        /// <summary>
        /// Get paginated projects with department and manager info
        /// </summary>
        public override async Task<PagedResult<Project>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<Project, bool>>? filter = null,
            Func<IQueryable<Project>, IOrderedQueryable<Project>>? orderBy = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IQueryable<Project> query = _context.Projects
                .AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.ProjectManager);

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
                // Default sorting by project code
                query = query.OrderBy(p => p.ProjectCode);
            }

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Project>(items, totalCount, pageNumber, pageSize);
        }

        public async Task InvalidateCacheAsync(int? projectId = null)
        {
            await _cacheService.RemoveByPrefixAsync("erp:proj:scope:");
            await _cacheService.RemoveAsync(CacheKeys.ProjectsAll);
            await _cacheService.RemoveAsync(CacheKeys.AvailableProjectManagersAll);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.ReportProjectsPrefix);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.ReportTasksPrefix);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.ReportDepartmentsPrefix);

            if (projectId.HasValue)
            {
                await _cacheService.RemoveAsync($"erp:proj:{projectId.Value}");
                await _cacheService.RemoveAsync($"erp:proj:{projectId.Value}:profile");
            }
        }

        private string BuildScopedProjectListKey()
        {
            if (IsCEO())
            {
                return "erp:proj:scope:ceo";
            }

            var managedDepartmentId = GetManagedDepartmentId();
            if (managedDepartmentId.HasValue)
            {
                return $"erp:proj:scope:manageddept:{managedDepartmentId.Value}";
            }

            var managedProjectId = GetManagedProjectId();
            if (managedProjectId.HasValue)
            {
                return $"erp:proj:scope:managedproj:{managedProjectId.Value}";
            }

            var userDepartmentId = GetUserDepartmentId();
            if (userDepartmentId.HasValue)
            {
                return $"erp:proj:scope:userdept:{userDepartmentId.Value}";
            }

            return "erp:proj:scope:none";
        }
    }
}
