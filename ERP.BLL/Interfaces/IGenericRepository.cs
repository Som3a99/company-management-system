using ERP.BLL.Common;
using ERP.DAL.Models;
using System.Linq.Expressions;

namespace ERP.BLL.Interfaces
{
    public interface IGenericRepository<T> where T : Base
    {

        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        
        /// <summary>
        /// Get entity by ID with tracking enabled (for update operations)
        /// </summary>
        Task<T?> GetByIdTrackedAsync(int id);
        
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(int id);
        
        /// <summary>
        /// Delete entity asynchronously (for async controller operations)
        /// </summary>
        Task DeleteAsync(int id);

        /// <summary>
        /// Get paginated results with optional filtering
        /// </summary>
        Task<PagedResult<T>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null);
    }
}
