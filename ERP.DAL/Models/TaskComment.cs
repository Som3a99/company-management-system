namespace ERP.DAL.Models
{
    public class TaskComment : Base
    {
        public int TaskId { get; set; }
        public TaskItem Task { get; set; } = null!;

        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
