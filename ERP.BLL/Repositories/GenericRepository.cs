using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Expressions;

namespace ERP.BLL.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : Base
    {
        private protected readonly ApplicationDbContext _context;

        public GenericRepository(ApplicationDbContext context)
        {
            _context=context;
        }
        public virtual async Task<IEnumerable<T>> GetAllAsync()
            => await _context.Set<T>()
                             .AsNoTracking()
                             .ToListAsync();
        public virtual async Task<T?> GetByIdAsync(int id)
            => await _context.Set<T>()
                             .AsNoTracking()
                             .FirstOrDefaultAsync(e => e.Id == id);

        /// <summary>
        /// Get entity by ID with tracking enabled (for update operations)
        /// </summary>
        public virtual async Task<T?> GetByIdTrackedAsync(int id)
            => await _context.Set<T>()
                             .IgnoreQueryFilters()
                             .FirstOrDefaultAsync(e => e.Id == id);

        public virtual async Task AddAsync(T entity)
        {
           await _context.AddAsync(entity);
        }
        public virtual void Update(T entity)
        {
            _context.Update(entity);
        }
        /// <summary>
        /// Delete entity asynchronously (soft delete)
        /// </summary>
        public virtual async Task DeleteAsync(int id)
        {
            var entity = await _context.Set<T>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == id);
            if (entity != null)
            {
                // Check if entity has IsDeleted property using reflection
                var isDeletedProp = typeof(T).GetProperty("IsDeleted");
                if (isDeletedProp != null && isDeletedProp.PropertyType == typeof(bool) && isDeletedProp.CanWrite)
                {
                    isDeletedProp.SetValue(entity, true);
                    _context.Update(entity);
                }
                else
                {
                    // Throw IsDeleted is not present
                    throw new InvalidOperationException($"Type {typeof(T).Name} does not support soft delete (missing IsDeleted property).");
                }
            }
        }

        /// <summary>
        ///Get paginated results with filtering and sorting
        /// </summary>
        public virtual async Task<PagedResult<T>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        {
            // Validate pagination parameters
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100; // Max page size limit

            IQueryable<T> query = _context.Set<T>().AsNoTracking();

            // Apply filter if provided
            if (filter != null)
            {
                query = query.Where(filter);
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            if (orderBy != null)
            {
                query = orderBy(query);
            }

            // Apply pagination
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>(items, totalCount, pageNumber, pageSize);
        }
    }
}
