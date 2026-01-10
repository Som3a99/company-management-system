using ERP.DAL.Models;

namespace ERP.BLL.Interfaces
{
    public interface IGenericRepository<T> where T : Base
    {
        IEnumerable<T> GetAll();
        T? GetById(int id);
        int Add(T entity);
        int Update(T entity);
        int Delete(int id);
    }
}
