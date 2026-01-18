using ERP.DAL.Models;

namespace ERP.BLL.Interfaces
{
    public interface IGenericRepository<T> where T : Base
    {

        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(int id);
    }
}
