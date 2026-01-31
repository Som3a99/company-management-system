using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace ERP.BLL.Interfaces
{
    public interface IDepartmentRepository : IGenericRepository<Department>
    {
        // Gets a department by its code, optionally excluding a specific department by ID
        Task<Department?> GetByManagerIdAsync(int managerId, int? excludeDepartmentId = null);

        /// <summary>
        ///  Check if department code exists
        /// </summary>
        Task<bool> DepartmentCodeExistsAsync(string code, int? excludeDepartmentId = null);

        /// <summary>
        /// Get department by code
        /// </summary>
        Task<Department?> GetByCodeAsync(string code);

        /// <summary>
        ///  Get department by manager id for update operation
        /// </summary>
        /// <param name="managerId"></param>
        /// <param name="excludeDepartmentId"></param>
        /// <returns></returns>
        Task<Department?> GetDepartmentByManagerForUpdateAsync(int managerId, int? excludeDepartmentId);

        /// <summary>
        /// Has active employees in department
        /// </summary>
        /// <param name="departmentId"></param>
        /// <returns></returns>
        Task<bool> HasActiveEmployeesAsync(int departmentId);

        /// <summary>
        /// Get projects by department ID
        /// </summary>
        Task<IEnumerable<Project>> GetProjectsByDepartmentAsync(int departmentId);

        /// <summary>
        /// Get department with all related data for profile page
        /// </summary>
        Task<Department?> GetDepartmentProfileAsync(int id);
    }
}
