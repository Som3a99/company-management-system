using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;

namespace ERP.BLL.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IEmployeeRepository _employeeRepository;

        public UnitOfWork(ApplicationDbContext context, IDepartmentRepository departmentRepository, IEmployeeRepository employeeRepository)
        {
            _context=context;
            _departmentRepository=departmentRepository;
            _employeeRepository=employeeRepository;
        }

        public IDepartmentRepository DepartmentRepository => _departmentRepository;

        public IEmployeeRepository EmployeeRepository => _employeeRepository;

        public int Complete()
        {
            return _context.SaveChanges();
        }
    }
}
