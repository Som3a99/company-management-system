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
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
