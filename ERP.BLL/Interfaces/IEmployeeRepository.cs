using ERP.DAL.Models;

namespace ERP.BLL.Interfaces
{
    public interface IEmployeeRepository : IGenericRepository<Employee>
    {
        /// <summary>
        /// Check if an email already exists in the database
        /// </summary>
        /// <param name="email">Email to check</param>
        /// <param name="excludeEmployeeId">Employee ID to exclude from check (for updates)</param>
        /// <returns>True if email exists, false otherwise</returns>
        Task<bool> EmailExistsAsync(string email, int? excludeEmployeeId = null);

        /// <summary>
        /// Get employee by email address
        /// </summary>
        Task<Employee?> GetByEmailAsync(string email);

        /// <summary>
        ///  Get all employees assigned to a specific project
        /// </summary>
        Task<IEnumerable<Employee>> GetEmployeesByProjectIdAsync(int projectId);

        /// <summary>
        /// Get all employees NOT assigned to any project (available for assignment)
        /// </summary>
        Task<IEnumerable<Employee>> GetUnassignedEmployeesAsync();

        /// <summary>
        /// Get employee with all related data for profile page
        /// </summary>
        Task<Employee?> GetEmployeeProfileAsync(int id);
    }
}