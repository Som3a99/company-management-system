using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ERP.BLL.Repositories
{
    public class DepartmentRepository : GenericRepository<Department>, IDepartmentRepository
    {
        public DepartmentRepository(ApplicationDbContext context) : base(context)
        {
        }

        // Override GetAll to include Employees navigation property
        public override async Task<IEnumerable<Department>> GetAllAsync()
        {
            return await _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Manager) // Include manager
                .ToListAsync();
        }

        // Override GetById to include Employees navigation property
        public override async Task<Department?> GetByIdAsync(int id)
        {
            return await _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Manager)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        /// <summary>
        /// Get department by ID with tracking enabled (for update operations)
        /// </summary>
        public override async Task<Department?> GetByIdTrackedAsync(int id)
        {
            return await _context.Departments
                .Include(d => d.Employees)
                .Include(d => d.Manager)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        /// <summary>
        /// Override Delete to implement soft delete
        /// </summary>
        public override void Delete(int id)
        {
            var department = _context.Departments.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
            if (department != null)
            {
                department.IsDeleted = true;
                _context.Update(department);
            }
        }

        /// <summary>
        /// Async soft delete for department
        /// </summary>
        public override async Task DeleteAsync(int id)
        {
            var department = await _context.Departments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == id);
            if (department != null)
            {
                department.IsDeleted = true;
                _context.Update(department);
            }
        }

        /// <summary>
        /// Gets a department by its manager ID, optionally excluding a specific department by ID
        ///  Exclude soft-deleted departments
        /// </summary>
        public async Task<Department?> GetByManagerIdAsync(int managerId, int? excludeDepartmentId = null)
        {
            var query = _context.Departments
                .AsNoTracking()
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
                .Where(d => d.DepartmentCode.Equals(normalizedCode, StringComparison.CurrentCultureIgnoreCase));

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
                .Include(d => d.Manager);

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
        /// <param name="managerId">The ID of the manager whose department is to be retrieved.</param>
        /// <param name="excludeDepartmentId">The ID of the department to exclude from the search.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the matching Department entity
        /// or null if not found.</returns>
        public async Task<Department?> GetDepartmentByManagerForUpdateAsync(int managerId, int excludeDepartmentId)
        {
            return await _context.Departments
                .FromSqlRaw(
                    @"SELECT * FROM Departments WITH (UPDLOCK)
                      WHERE ManagerId = {0}
                      AND Id != {1}
                      AND IsDeleted = 0",
                    managerId,
                    excludeDepartmentId)
                .AsTracking()
                .FirstOrDefaultAsync();
        }

    }
}
