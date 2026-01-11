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
        public override IEnumerable<Department> GetAll()
        {
            return _context.Departments
                .Include(d => d.Employees)
                .ToList();
        }

        // Override GetById to include Employees navigation property
        public override Department? GetById(int id)
        {
            return _context.Departments
                .Include(d => d.Employees)
                .FirstOrDefault(d => d.Id == id);
        }
    }
}
