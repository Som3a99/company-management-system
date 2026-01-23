using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;

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
                .Where(e => !e.IsDeleted)
                .ToListAsync();
        }

        // Override GetById to include Department navigation property
        public override async Task<Employee?> GetByIdAsync(int id)
        {
            return await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        }

        // Override Delete to implement soft delete
        public override void Delete(int id)
        {
            var employee = _context.Employees.Find(id);
            if (employee != null)
            {
                employee.IsDeleted = true;
                _context.Update(employee);
            }
        }
    }
}
