using ERP.BLL.Reporting.Dtos;
using ERP.BLL.Reporting.Interfaces;
using ERP.DAL.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.BLL.Reporting.Services
{
    public sealed class ReportingService : IReportingService
    {
        private readonly ApplicationDbContext _dbContext;

        public ReportingService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<TaskReportRowDto>> GetTaskReportAsync(ReportRequestDto request, int? scopedDepartmentId, int? scopedProjectId)
        {
            var query = _dbContext.TaskItems
                .AsNoTracking()
                .Include(t => t.Project)
                .ThenInclude(p => p.Department)
                .Include(t => t.AssignedToEmployee)
                .AsQueryable();

            if (request.StartDateUtc.HasValue)
                query = query.Where(t => t.CreatedAt >= request.StartDateUtc.Value);

            if (request.EndDateUtc.HasValue)
                query = query.Where(t => t.CreatedAt <= request.EndDateUtc.Value);

            if (request.DepartmentId.HasValue)
                query = query.Where(t => t.Project != null && t.Project.DepartmentId == request.DepartmentId.Value);

            if (request.ProjectId.HasValue)
                query = query.Where(t => t.ProjectId == request.ProjectId.Value);

            if (scopedDepartmentId.HasValue)
                query = query.Where(t => t.Project != null && t.Project.DepartmentId == scopedDepartmentId.Value);

            if (scopedProjectId.HasValue)
                query = query.Where(t => t.ProjectId == scopedProjectId.Value);

            return await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TaskReportRowDto
                {
                    TaskId = t.Id,
                    Title = t.Title,
                    Status = t.Status.ToString(),
                    Priority = t.Priority.ToString(),
                    Project = t.Project != null ? t.Project.ProjectName : null,
                    Assignee = t.AssignedToEmployee != null ? (t.AssignedToEmployee.FirstName + " " + t.AssignedToEmployee.LastName) : null,
                    DueDateUtc = t.DueDate,
                    EstimatedHours = t.EstimatedHours,
                    ActualHours = t.ActualHours
                })
                .Take(1000)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ProjectReportRowDto>> GetProjectReportAsync(ReportRequestDto request, int? scopedDepartmentId, int? scopedProjectId)
        {
            var query = _dbContext.Projects
                .AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.Tasks)
                .AsQueryable();

            if (request.DepartmentId.HasValue)
                query = query.Where(p => p.DepartmentId == request.DepartmentId.Value);

            if (request.ProjectId.HasValue)
                query = query.Where(p => p.Id == request.ProjectId.Value);

            if (request.StartDateUtc.HasValue)
                query = query.Where(p => p.StartDate >= request.StartDateUtc.Value);

            if (request.EndDateUtc.HasValue)
                query = query.Where(p => p.StartDate <= request.EndDateUtc.Value);

            if (scopedDepartmentId.HasValue)
                query = query.Where(p => p.DepartmentId == scopedDepartmentId.Value);

            if (scopedProjectId.HasValue)
                query = query.Where(p => p.Id == scopedProjectId.Value);

            return await query
                .OrderBy(p => p.ProjectCode)
                .Select(p => new ProjectReportRowDto
                {
                    ProjectId = p.Id,
                    ProjectCode = p.ProjectCode,
                    ProjectName = p.ProjectName,
                    DepartmentName = p.Department.DepartmentName,
                    Status = p.Status.ToString(),
                    Budget = p.Budget,
                    TotalTasks = p.Tasks.Count,
                    CompletedTasks = p.Tasks.Count(t => t.Status == TaskStatus.Completed)
                })
                .Take(1000)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DepartmentReportRowDto>> GetDepartmentReportAsync(ReportRequestDto request, int? scopedDepartmentId)
        {
            var query = _dbContext.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Projects)
                .ThenInclude(p => p.Tasks)
                .AsQueryable();

            if (request.DepartmentId.HasValue)
                query = query.Where(d => d.Id == request.DepartmentId.Value);

            if (scopedDepartmentId.HasValue)
                query = query.Where(d => d.Id == scopedDepartmentId.Value);

            return await query
                .OrderBy(d => d.DepartmentCode)
                .Select(d => new DepartmentReportRowDto
                {
                    DepartmentId = d.Id,
                    DepartmentCode = d.DepartmentCode,
                    DepartmentName = d.DepartmentName,
                    EmployeesCount = d.Employees.Count,
                    ProjectsCount = d.Projects.Count,
                    OpenTasksCount = d.Projects.SelectMany(p => p.Tasks).Count(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled)
                })
                .Take(500)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<AuditReportRowDto>> GetAuditReportAsync(ReportRequestDto request)
        {
            var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

            if (request.StartDateUtc.HasValue)
                query = query.Where(a => a.Timestamp >= request.StartDateUtc.Value);

            if (request.EndDateUtc.HasValue)
                query = query.Where(a => a.Timestamp <= request.EndDateUtc.Value);

            return await query
                .OrderByDescending(a => a.Timestamp)
                .Select(a => new AuditReportRowDto
                {
                    TimestampUtc = a.Timestamp,
                    UserEmail = a.UserEmail,
                    Action = a.Action,
                    ResourceType = a.ResourceType,
                    ResourceId = a.ResourceId,
                    Succeeded = a.Succeeded,
                    ErrorMessage = a.ErrorMessage
                })
                .Take(1000)
                .ToListAsync();
        }
    }
}