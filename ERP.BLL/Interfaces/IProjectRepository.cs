using ERP.DAL.Models;

namespace ERP.BLL.Interfaces
{
    public interface IProjectRepository : IGenericRepository<Project>
    {
        /// <summary>
        /// Check if project code exists (case-insensitive)
        /// </summary>
        Task<bool> ProjectCodeExistsAsync(string code, int? excludeProjectId = null);

        /// <summary>
        /// Get project by code
        /// </summary>
        Task<Project?> GetByCodeAsync(string code);

        /// <summary>
        /// Get project by manager ID for update operation with locking
        /// </summary>
        Task<Project?> GetProjectByManagerForUpdateAsync(int managerId, int? excludeProjectId = null);

        /// <summary>
        /// Check if employee is already managing a project
        /// </summary>
        Task<bool> IsEmployeeManagingProjectAsync(int employeeId, int? excludeProjectId = null);

        /// <summary>
        /// Get all projects for a specific department
        /// </summary>
        Task<IEnumerable<Project>> GetProjectsByDepartmentAsync(int departmentId);

        /// <summary>
        /// Get projects by status
        /// </summary>
        Task<IEnumerable<Project>> GetProjectsByStatusAsync(ProjectStatus status);

        /// <summary>
        /// Get all employees assigned to a specific project
        /// </summary>
        Task<IEnumerable<Employee>> GetEmployeesByProjectAsync(int projectId);

        /// <summary>
        /// Check if employee is already assigned to a project
        /// </summary>
        Task<bool> IsEmployeeAssignedToProjectAsync(int employeeId, int? excludeProjectId = null);

        /// <summary>
        /// Get project with all employees included
        /// </summary>
        Task<Project?> GetProjectWithEmployeesAsync(int projectId);

        /// <summary>
        /// Get employees assigned to a specific project as IQueryable
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        Task<IQueryable<Employee>> GetEmployeesByProjectQueryableAsync(int projectId);

        /// <summary>
        /// Get project with all related data for profile page
        /// </summary>
        Task<Project?> GetProjectProfileAsync(int id);

        /// <summary>
        /// Evicts department aggregate/list/profile cache entries after successful writes.
        /// </summary>
        Task InvalidateCacheAsync(int? projectId = null);

    }
}
