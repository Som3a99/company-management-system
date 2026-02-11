using ERP.PL.ViewModels.Employee;

namespace ERP.PL.Services
{
    public interface IProjectTeamService
    {
        Task<(bool Succeeded, string Message)> AssignEmployeeAsync(int projectId, int employeeId, int currentEmployeeId, string performedByUserId, string performedBy, bool isCeo, CancellationToken cancellationToken = default);
        Task<(bool Succeeded, string Message)> RemoveEmployeeAsync(int projectId, int employeeId, int currentEmployeeId, string performedByUserId, string performedBy, bool isCeo, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EmployeeViewModel>> GetEligibleEmployeesAsync(int projectId, int currentEmployeeId, bool isCeo, CancellationToken cancellationToken = default);
    }
}
