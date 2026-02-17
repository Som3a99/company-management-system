namespace ERP.DAL.Models
{
    public class SeedHistory : Base 
    {
        public string SeedVersion { get; set; } = null!;
        public DateTime SeededAt { get; set; }
        public string Environment { get; set; } = null!;
        public bool IsSuccessful { get; set; }
        public string? Notes { get; set; }
    }
}
