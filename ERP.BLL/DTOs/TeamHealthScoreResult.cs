namespace ERP.BLL.DTOs
{
    public class TeamHealthScoreResult
    {
        public int Score { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> RiskFactors { get; set; } = new();
    }
}
