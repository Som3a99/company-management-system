using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Expressions;

namespace ERP.BLL.Repositories
{
    public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
    {
        public EmployeeRepository(ApplicationDbContext context) : base(context)
        {
        }

        // Override GetAll to include Department navigation property
        public override async Task<IEnumerable<Employee>> GetAllAsync()
        {
            return await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .ToListAsync();
        }

        // Override GetById to include Department navigation property
        public override async Task<Employee?> GetByIdAsync(int id)
        {
            return await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
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

            IQueryable<Employee> query = _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => !e.IsDeleted);

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
