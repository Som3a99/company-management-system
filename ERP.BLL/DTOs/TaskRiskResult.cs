namespace ERP.BLL.DTOs
{
    public class TaskRiskResult
    {
        public int Score { get; set; }      // 0–100
        public string Level { get; set; }   // Low | Medium | High
        public string Reason { get; set; }  // explanation text

        public TaskRiskResult()
        {
            Level = "Low";
            Reason = string.Empty;
        }
    }
}
