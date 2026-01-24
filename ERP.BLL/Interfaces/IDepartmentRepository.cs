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

    }
}
