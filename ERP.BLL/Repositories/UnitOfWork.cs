using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.BLL.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ITaskRepository _taskRepository;
        private bool _disposed = false;

        public UnitOfWork(ApplicationDbContext context, IDepartmentRepository departmentRepository, IEmployeeRepository employeeRepository, IProjectRepository projectRepository, ITaskRepository taskRepository)
        {
            _context=context;
            _departmentRepository=departmentRepository;
            _employeeRepository=employeeRepository;
            _projectRepository=projectRepository;
            _taskRepository=taskRepository;
        }

        public IDepartmentRepository DepartmentRepository => _departmentRepository;

        public IEmployeeRepository EmployeeRepository => _employeeRepository;
        public IProjectRepository ProjectRepository => _projectRepository;
        public ITaskRepository TaskRepository => _taskRepository;

        public async Task<int> CompleteAsync()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Log the error (if logger is available)
                // _logger?.LogError(ex, "Error saving changes to database");
                throw; // Re-throw to let controller handle it
            }
        }

        /// <summary>
        /// Begin a database transaction
        /// Use this for operations that need to be atomic (all succeed or all fail)
        /// </summary>
        /// <returns>Transaction object to commit or rollback</returns>
        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

        /// <summary>
        ///  Proper disposal pattern implementation
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _context?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
