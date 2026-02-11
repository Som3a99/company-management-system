using ERP.PL.ViewModels.Employee;

namespace ERP.PL.Services
{
    public interface IDepartmentManagerHrService
    {
        Task<IReadOnlyList<EmployeeViewModel>> GetDepartmentEmployeesAsync(int managerDepartmentId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EmployeeViewModel>> GetUnassignedEmployeesAsync(CancellationToken cancellationToken = default);
        Task<(bool Succeeded, string Message)> AssignEmployeeAsync(int employeeId, int managerDepartmentId, string userId, string userEmail, CancellationToken cancellationToken = default);
        Task<(bool Succeeded, string Message)> RemoveEmployeeAsync(int employeeId, int managerDepartmentId, string userId, string userEmail, CancellationToken cancellationToken = default);
    }
}
