using ERP.DAL.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ERP.PL.ViewModels.Tasks
{
    public class TaskUpsertViewModel
    {
        public int? Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ProjectId { get; set; }
        public int? AssignedToEmployeeId { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal? EstimatedHours { get; set; }
        public string? RowVersionBase64 { get; set; }

        public List<SelectListItem> ProjectOptions { get; set; } = new();
        public List<SelectListItem> AssigneeOptions { get; set; } = new();
    }
}
