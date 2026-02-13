using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.BLL.Repositories
{
    public class TaskRepository : GenericRepository<TaskItem>, ITaskRepository
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TaskRepository(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor) : base(context)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<TaskItem?> GetTaskWithScopeDataAsync(int taskId)
        {
            return await _context.TaskItems
                .Include(t => t.Project)
                .ThenInclude(p => p.ProjectManager)
                .Include(t => t.AssignedToEmployee)
                .ThenInclude(e => e.ApplicationUser)
                .Include(t => t.Comments)
                .FirstOrDefaultAsync(t => t.Id == taskId);
        }

        public async Task<TaskItem?> GetTaskByIdWithDetailsAsync(int taskId)
        {
            return await _context.TaskItems
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.AssignedToEmployee)
                .ThenInclude(e => e.ApplicationUser)
                .Include(t => t.Comments.OrderByDescending(c => c.CreatedAt))
                .ThenInclude(c => c.User)
                .ThenInclude(u => u.Employee)
                .FirstOrDefaultAsync(t => t.Id == taskId);
        }

        public async Task<bool> IsEmployeeAssignedToProjectAsync(int employeeId, int projectId)
        {
            return await _context.ProjectEmployees
                .AsNoTracking()
                .AnyAsync(pe => pe.ProjectId == projectId && pe.EmployeeId == employeeId)
                || await _context.Employees.AsNoTracking()
                                           .AnyAsync(e => e.Id == employeeId && e.ProjectId == projectId && e.IsActive && !e.IsDeleted);
        }

        public async Task<PagedResult<TaskItem>> GetTasksPagedAsync(
                        int pageNumber,
                        int pageSize,
                        int? projectId,
                        TaskStatus? status,
                        int? assigneeEmployeeId,
                        string? sortBy,
                        bool descending,
                        string currentUserId)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var user = _httpContextAccessor.HttpContext?.User;
            var isCeo = user?.IsInRole("CEO") == true;
            var managedDepartmentId = ParseClaim("ManagedDepartmentId");
            var managerEmployeeId = ParseClaim("EmployeeId");
            var isProjectManager = user?.IsInRole("ProjectManager") == true;
            IQueryable<TaskItem> query = _context.TaskItems.AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.AssignedToEmployee)
                .ThenInclude(e => e.ApplicationUser);

            if (!isCeo)
            {
                if (isProjectManager && managerEmployeeId.HasValue)
                {
                    query = query.Where(t =>
                        (t.Project != null && t.Project.ProjectManagerId == managerEmployeeId.Value)
                        || (t.AssignedToEmployee != null && t.AssignedToEmployee.ApplicationUserId == currentUserId));
                }
                else if (managedDepartmentId.HasValue)
                {
                    query = query.Where(t =>
                        t.AssignedToEmployee != null && t.AssignedToEmployee.DepartmentId == managedDepartmentId.Value);
                }
                else
                {
                    query = query.Where(t => t.AssignedToEmployee != null && t.AssignedToEmployee.ApplicationUserId == currentUserId);
                }
            }

            if (projectId.HasValue)
                query = query.Where(t => t.ProjectId == projectId.Value);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            if (assigneeEmployeeId.HasValue)
                query = query.Where(t => t.AssignedToEmployeeId == assigneeEmployeeId.Value);

            query = (sortBy?.ToLowerInvariant(), descending) switch
            {
                ("priority", true) => query.OrderByDescending(t => t.Priority).ThenByDescending(t => t.Id),
                ("priority", false) => query.OrderBy(t => t.Priority).ThenBy(t => t.Id),
                ("status", true) => query.OrderByDescending(t => t.Status).ThenByDescending(t => t.Id),
                ("status", false) => query.OrderBy(t => t.Status).ThenBy(t => t.Id),
                ("createdat", true) => query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id),
                ("createdat", false) => query.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id),
                ("duedate", true) => query.OrderByDescending(t => t.DueDate).ThenByDescending(t => t.Id),
                _ => query.OrderBy(t => t.DueDate).ThenBy(t => t.Id)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<TaskItem>(items, totalCount, pageNumber, pageSize);
        }

        private int? ParseClaim(string claimType)
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
            return int.TryParse(value, out var parsed) ? parsed : null;
        }
    }
}
