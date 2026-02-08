namespace ERP.PL.ViewModels.UserManagement
{
    /// <summary>
    /// ViewModel for listing employees without user accounts
    /// </summary>
    public class EmployeeWithoutAccountViewModel
    {
        public int EmployeeId { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string FullName => $"{FirstName} {LastName}";
        public string Email { get; set; } = null!;
        public string Position { get; set; } = null!;
        public string? DepartmentName { get; set; }
        public bool IsDepartmentManager { get; set; }
        public bool IsProjectManager { get; set; }
        public DateTime HireDate { get; set; }
    }
}
