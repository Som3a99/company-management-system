using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace ERP.BLL.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : Base
    {
        private protected readonly ApplicationDbContext _context;

        public GenericRepository(ApplicationDbContext context)
        {
            _context=context;
        }
        public virtual IEnumerable<T> GetAll()
            => _context.Set<T>().AsNoTracking().ToList();
        public virtual T? GetById(int id)
            => _context.Set<T>().Find(id);

        public virtual int Add(T entity)
        {
            _context.Add(entity);
            return _context.SaveChanges();
        }
        public virtual int Update(T entity)
        {
            _context.Update(entity);
            return _context.SaveChanges();
        }

        public virtual int Delete(int id)
        {
            var entity = _context.Set<T>().Find(id);
            if (entity != null)
            {
                _context.Set<T>().Remove(entity);
                return _context.SaveChanges();
            }
            return 0;
        }
    }
}
