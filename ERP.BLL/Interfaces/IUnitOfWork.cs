using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.BLL.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        public IDepartmentRepository DepartmentRepository { get; }
        public IEmployeeRepository EmployeeRepository { get; }
        public IProjectRepository ProjectRepository { get; }
        public ITaskRepository TaskRepository { get; }
        Task<int> CompleteAsync();

        [Obsolete("Use ExecuteInTransactionAsync instead when EnableRetryOnFailure is configured.")]
        Task<IDbContextTransaction> BeginTransactionAsync();

        /// <summary>
        /// Execute operations inside a transaction compatible with EF Core retry execution strategy.
        /// </summary>
        Task ExecuteInTransactionAsync(Func<Task> action);

        /// <summary>
        /// Execute operations inside a transaction compatible with EF Core retry execution strategy.
        /// Returns a result from the transactional work.
        /// </summary>
        Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action);
    }
}
