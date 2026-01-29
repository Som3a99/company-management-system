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
    }
}
