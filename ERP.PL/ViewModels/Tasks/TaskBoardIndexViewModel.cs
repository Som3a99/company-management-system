using ERP.DAL.Models;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.PL.ViewModels.Tasks
{
    public class TaskBoardIndexViewModel
    {
        public IReadOnlyList<TaskItem> Tasks { get; set; } = Array.Empty<TaskItem>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }

        public int? ProjectId { get; set; }
        public int? AssigneeEmployeeId { get; set; }
        public TaskStatus? Status { get; set; }
        public string? SortBy { get; set; }
        public bool Descending { get; set; }

        public int NewCount { get; set; }
        public int InProgressCount { get; set; }
        public int BlockedCount { get; set; }
        public int CompletedCount { get; set; }
    }
}
