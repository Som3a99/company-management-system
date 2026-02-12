using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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
                .FirstOrDefaultAsync(t => t.Id == taskId);
        }

        public async Task<bool> IsEmployeeAssignedToProjectAsync(int employeeId, int projectId)
        {
            return await _context.Employees
                .AsNoTracking()
                .AnyAsync(e => e.Id == employeeId && e.ProjectId == projectId && e.IsActive && !e.IsDeleted);
        }

        public async Task<IEnumerable<TaskItem>> GetVisibleTasksAsync(string userId)
        {
            var query = _context.TaskItems
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.AssignedToEmployee)
                .ThenInclude(e => e.ApplicationUser)
                .AsQueryable();

            if (_httpContextAccessor.HttpContext?.User.IsInRole("CEO") == true)
            {
                return await query.OrderBy(t => t.DueDate).ThenBy(t => t.Id).ToListAsync();
            }

            var managedProjectIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("ManagedProjectId")?.Value;
            if (int.TryParse(managedProjectIdClaim, out var managedProjectId))
            {
                return await query.Where(t => t.ProjectId == managedProjectId)
                    .OrderBy(t => t.DueDate).ThenBy(t => t.Id)
                    .ToListAsync();
            }

            return await query.Where(t => t.AssignedToEmployee.ApplicationUserId == userId)
                .OrderBy(t => t.DueDate).ThenBy(t => t.Id)
                .ToListAsync();
        }
    }
}
