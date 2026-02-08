namespace ERP.PL.Services
{
    public interface IRoleManagementService
    {
        /// <summary>
        /// Sync employee roles based on current status
        /// (DepartmentManager, ProjectManager)
        /// </summary>
        Task SyncEmployeeRolesAsync(int employeeId);

        /// <summary>
        /// Remove management roles if employee no longer a manager
        /// </summary>
        Task RemoveManagementRolesAsync(string applicationUserId);
    }
}
