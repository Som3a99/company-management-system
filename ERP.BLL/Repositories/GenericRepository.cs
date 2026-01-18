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
            => _context.Set<T>().AsNoTracking().FirstOrDefault(e => e.Id == id);

        public virtual void Add(T entity)
        {
            _context.Add(entity);
        }
        public virtual void Update(T entity)
        {
            _context.Update(entity);
        }

        public virtual void Delete(int id)
        {
            var entity = _context.Set<T>().Find(id);
            if (entity != null)
            {
                _context.Set<T>().Remove(entity);
            }
        }
    }
}
