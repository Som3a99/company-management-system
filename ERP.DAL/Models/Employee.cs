namespace ERP.DAL.Models
{
    public class Employee : Base
    {
        #region Properties
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Position { get; set; } = null!;
        public DateTime HireDate { get; set; }
        public decimal Salary { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        #endregion

        #region Navigational Property
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;
        #endregion
    }
}
