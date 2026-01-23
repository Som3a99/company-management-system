using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;

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
                .Include(d => d.Employees.Where(e => !e.IsDeleted)) // Filter deleted employees
                .Include(d => d.Manager) // Include manager
                .ToListAsync();
        }

        // Override GetById to include Employees navigation property
        public override async Task<Department?> GetByIdAsync(int id)
        {
            return await _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees.Where(e => !e.IsDeleted))
                .Include(d => d.Manager)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        // Gets a department by its manager ID, optionally excluding a specific department by ID
        public async Task<Department?> GetByManagerIdAsync(int managerId, int? excludeDepartmentId = null)
        {
            var query = _context.Departments
                .AsNoTracking()
                .Where(d => d.ManagerId == managerId);

            if (excludeDepartmentId.HasValue)
                query = query.Where(d => d.Id != excludeDepartmentId.Value);

            return await query.FirstOrDefaultAsync();
        }
    }
}
