using ERP.DAL.Models;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.PL.ViewModels.Tasks
{
    public class TaskDetailsViewModel
    {
        public TaskItem Task { get; set; } = null!;
        public string? StatusError { get; set; }
        public string? CommentError { get; set; }

        public TaskStatus NewStatus { get; set; }
        public string? RowVersionBase64 { get; set; }
        public string? CommentContent { get; set; }
    }
}
