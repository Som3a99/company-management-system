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
        public virtual async Task<IEnumerable<T>> GetAllAsync()
            => await _context.Set<T>()
                             .AsNoTracking()
                             .ToListAsync();
        public virtual async Task<T?> GetByIdAsync(int id)
            => await _context.Set<T>()
                             .AsNoTracking()
                             .FirstOrDefaultAsync(e => e.Id == id);

        public virtual async Task AddAsync(T entity)
        {
           await _context.AddAsync(entity);
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
