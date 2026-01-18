using ERP.DAL.Models;

namespace ERP.BLL.Interfaces
{
    public interface IGenericRepository<T> where T : Base
    {
        IEnumerable<T> GetAll();
        T? GetById(int id);
        void Add(T entity);
        void Update(T entity);
        void Delete(int id);
    }
}
