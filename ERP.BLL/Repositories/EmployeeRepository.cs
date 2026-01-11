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
        public override IEnumerable<Employee> GetAll()
        {
            return _context.Employees
                .Include(e => e.Department)
                .Where(e => !e.IsDeleted)
                .ToList();
        }

        // Override GetById to include Department navigation property
        public override Employee? GetById(int id)
        {
            return _context.Employees
                .Include(e => e.Department)
                .FirstOrDefault(e => e.Id == id && !e.IsDeleted);
        }
    }
}
