using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
namespace ERP.BLL.Repositories
{
    public class ProjectRepository : GenericRepository<Project>, IProjectRepository
    {
        public ProjectRepository(ApplicationDbContext context) : base(context)
        {
        }

        // Override GetAll to include navigation properties
        public override async Task<IEnumerable<Project>> GetAllAsync()
        {
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.ProjectManager)
                .ToListAsync();
        }

        // Override GetById to include navigation properties
        public override async Task<Project?> GetByIdAsync(int id)
        {
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.ProjectManager)
                .FirstOrDefaultAsync(p => p.Id == id);
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
    }
}
