namespace ERP.DAL.Models
{
    public class Department : Base
    {
        #region Properties
        public string DepartmentCode { get; set; } = null!;
        public string DepartmentName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        #endregion
    }
}
