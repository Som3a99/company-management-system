using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using Microsoft.EntityFrameworkCore;
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
        /// Begin a database transaction.
        /// IMPORTANT: Prefer <see cref="ExecuteInTransactionAsync"/> when using
        /// SqlServerRetryingExecutionStrategy (EnableRetryOnFailure).
        /// Direct BeginTransactionAsync is NOT compatible with retry strategies.
        /// </summary>
        [Obsolete("Use ExecuteInTransactionAsync instead when EnableRetryOnFailure is configured.")]
        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

        /// <summary>
        /// Execute operations inside a transaction that is compatible with
        /// SqlServerRetryingExecutionStrategy. All operations within the action
        /// are executed as a retriable atomic unit.
        /// </summary>
        /// <param name="action">The transactional operations to execute.</param>
        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                await action();
                await transaction.CommitAsync();
            });
        }

        /// <summary>
        /// Execute operations inside a transaction that is compatible with
        /// SqlServerRetryingExecutionStrategy. Returns a result from the transactional work.
        /// </summary>
        /// <typeparam name="TResult">The type of result returned.</typeparam>
        /// <param name="action">The transactional operations to execute.</param>
        /// <returns>The result of the transactional operations.</returns>
        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                var result = await action();
                await transaction.CommitAsync();
                return result;
            });
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
