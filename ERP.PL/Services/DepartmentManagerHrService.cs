using AutoMapper;
using ERP.BLL.Interfaces;
using ERP.PL.ViewModels.Employee;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Services
{
    public class DepartmentManagerHrService : IDepartmentManagerHrService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;

        public DepartmentManagerHrService(IUnitOfWork unitOfWork, IMapper mapper, IAuditService auditService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _auditService = auditService;
        }

        public async Task<IReadOnlyList<EmployeeViewModel>> GetDepartmentEmployeesAsync(int managerDepartmentId, CancellationToken cancellationToken = default)
        {
            var employees = await _unitOfWork.EmployeeRepository.GetPagedAsync(
                pageNumber: 1,
                pageSize: 100,
                filter: e => e.DepartmentId == managerDepartmentId && !e.IsDeleted,
                orderBy: q => q.OrderBy(e => e.LastName).ThenBy(e => e.FirstName));

            return _mapper.Map<IReadOnlyList<EmployeeViewModel>>(employees.Items);
        }

        public async Task<IReadOnlyList<EmployeeViewModel>> GetUnassignedEmployeesAsync(CancellationToken cancellationToken = default)
        {
            var employees = await _unitOfWork.EmployeeRepository.GetPagedAsync(
                pageNumber: 1,
                pageSize: 100,
                filter: e => !e.DepartmentId.HasValue && !e.IsDeleted,
                orderBy: q => q.OrderBy(e => e.LastName).ThenBy(e => e.FirstName));

            return _mapper.Map<IReadOnlyList<EmployeeViewModel>>(employees.Items);
        }

        public async Task<(bool Succeeded, string Message)> AssignEmployeeAsync(int employeeId, int managerDepartmentId, string userId, string userEmail, CancellationToken cancellationToken = default)
        {
            var employee = await _unitOfWork.EmployeeRepository.GetByIdTrackedAsync(employeeId);
            if (employee is null || employee.IsDeleted)
            {
                return (false, "Employee not found.");
            }

            if (employee.DepartmentId.HasValue)
            {
                await _auditService.LogAsync(userId, userEmail, "DEPARTMENT_EMPLOYEE_ASSIGN_DENIED", "Employee", employeeId,
                    succeeded: false, errorMessage: "Employee already assigned", details: $"TargetDepartmentId={managerDepartmentId}");
                return (false, "Only employees without a department can be assigned.");
            }

            employee.DepartmentId = managerDepartmentId;

            try
            {
                _unitOfWork.EmployeeRepository.Update(employee);
                await _unitOfWork.CompleteAsync();

                await _auditService.LogAsync(userId, userEmail, "DEPARTMENT_EMPLOYEE_ASSIGN", "Employee", employeeId,
                    details: $"ActionType=Assign;EmployeeId={employeeId};DepartmentId={managerDepartmentId}");
                return (true, "Employee assigned to your department successfully.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "Concurrency conflict. Please refresh and try again.");
            }
        }

        public async Task<(bool Succeeded, string Message)> RemoveEmployeeAsync(int employeeId, int managerDepartmentId, string userId, string userEmail, CancellationToken cancellationToken = default)
        {
            var employee = await _unitOfWork.EmployeeRepository.GetByIdTrackedAsync(employeeId);
            if (employee is null || employee.IsDeleted)
            {
                return (false, "Employee not found.");
            }

            if (employee.DepartmentId != managerDepartmentId)
            {
                await _auditService.LogAsync(userId, userEmail, "DEPARTMENT_EMPLOYEE_REMOVE_DENIED", "Employee", employeeId,
                    succeeded: false, errorMessage: "Cross-department tampering", details: $"ManagerDepartmentId={managerDepartmentId};EmployeeDepartmentId={employee.DepartmentId}");
                return (false, "Unauthorized department operation.");
            }

            employee.DepartmentId = null;

            try
            {
                _unitOfWork.EmployeeRepository.Update(employee);
                await _unitOfWork.CompleteAsync();

                await _auditService.LogAsync(userId, userEmail, "DEPARTMENT_EMPLOYEE_REMOVE", "Employee", employeeId,
                    details: $"ActionType=Remove;EmployeeId={employeeId};DepartmentId={managerDepartmentId}");
                return (true, "Employee removed from your department.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "Concurrency conflict. Please refresh and try again.");
            }
        }
    }
}
