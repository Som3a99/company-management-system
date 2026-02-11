using AutoMapper;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Employee;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Services
{
    public class ProjectTeamService : IProjectTeamService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;

        public ProjectTeamService(ApplicationDbContext context, IMapper mapper, IAuditService auditService)
        {
            _context = context;
            _mapper = mapper;
            _auditService = auditService;
        }

        public async Task<IReadOnlyList<EmployeeViewModel>> GetEligibleEmployeesAsync(int projectId, int currentEmployeeId, bool isCeo, CancellationToken cancellationToken = default)
        {
            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);

            if (project == null)
            {
                return Array.Empty<EmployeeViewModel>();
            }

            if (!isCeo && project.ProjectManagerId != currentEmployeeId)
            {
                return Array.Empty<EmployeeViewModel>();
            }

            var assignedEmployeeIds = await _context.ProjectEmployees
                .Where(pe => pe.ProjectId == projectId)
                .Select(pe => pe.EmployeeId)
                .ToListAsync(cancellationToken);

            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => !e.IsDeleted && e.IsActive && !assignedEmployeeIds.Contains(e.Id))
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Take(200)
                .ToListAsync(cancellationToken);

            return _mapper.Map<IReadOnlyList<EmployeeViewModel>>(employees);
        }

        public async Task<(bool Succeeded, string Message)> AssignEmployeeAsync(int projectId, int employeeId, int currentEmployeeId, string performedByUserId, string performedBy, bool isCeo, CancellationToken cancellationToken = default)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);

            if (project == null)
            {
                return (false, "Project not found.");
            }

            if (!isCeo && project.ProjectManagerId != currentEmployeeId)
            {
                await _auditService.LogAsync(performedByUserId, performedBy, "PROJECT_EMPLOYEE_ASSIGN_DENIED", "Project", projectId,
                    succeeded: false, errorMessage: "Cross-project tampering", details: $"ActionType=Assign;ProjectId={projectId};EmployeeId={employeeId}");
                return (false, "Unauthorized project operation.");
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, cancellationToken);

            if (employee == null)
            {
                return (false, "Employee not found.");
            }

            var exists = await _context.ProjectEmployees
                .AnyAsync(pe => pe.ProjectId == projectId && pe.EmployeeId == employeeId, cancellationToken);

            if (exists)
            {
                return (false, "Employee is already assigned to this project.");
            }

            _context.ProjectEmployees.Add(new ProjectEmployee
            {
                ProjectId = projectId,
                EmployeeId = employeeId,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = performedByUserId
            });

            // Keep legacy compatibility for current screens without changing department/role
            employee.ProjectId = projectId;

            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(performedByUserId, performedBy, "PROJECT_EMPLOYEE_ASSIGN", "Project", projectId,
                details: $"ActionType=Assign;ProjectId={projectId};EmployeeId={employeeId}");

            return (true, "Employee assigned to project successfully.");
        }

        public async Task<(bool Succeeded, string Message)> RemoveEmployeeAsync(int projectId, int employeeId, int currentEmployeeId, string performedByUserId, string performedBy, bool isCeo, CancellationToken cancellationToken = default)
        {
            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);

            if (project == null)
            {
                return (false, "Project not found.");
            }

            if (!isCeo && project.ProjectManagerId != currentEmployeeId)
            {
                await _auditService.LogAsync(performedByUserId, performedBy, "PROJECT_EMPLOYEE_REMOVE_DENIED", "Project", projectId,
                    succeeded: false, errorMessage: "Cross-project tampering", details: $"ActionType=Remove;ProjectId={projectId};EmployeeId={employeeId}");
                return (false, "Unauthorized project operation.");
            }

            var relation = await _context.ProjectEmployees
                .FirstOrDefaultAsync(pe => pe.ProjectId == projectId && pe.EmployeeId == employeeId, cancellationToken);

            if (relation == null)
            {
                return (false, "Employee is not assigned to this project.");
            }

            _context.ProjectEmployees.Remove(relation);

            // Keep legacy compatibility
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, cancellationToken);
            if (employee != null && employee.ProjectId == projectId)
            {
                employee.ProjectId = null;
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(performedByUserId, performedBy, "PROJECT_EMPLOYEE_REMOVE", "Project", projectId,
                details: $"ActionType=Remove;ProjectId={projectId};EmployeeId={employeeId}");

            return (true, "Employee removed from project successfully.");
        }
    }
}
