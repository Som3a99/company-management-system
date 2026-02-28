using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.DAL.Models
{
    public enum TaskPriority
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum TaskStatus
    {
        None = 0,
        New = 1,
        InProgress = 2,
        Blocked = 3,
        Completed = 4,
        Cancelled = 5
    }

    public class TaskItem : Base
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public int? ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public int? AssignedToEmployeeId { get; set; }
        public Employee AssignedToEmployee { get; set; } = null!;

        public string CreatedByUserId { get; set; } = null!;
        public ApplicationUser CreatedByUser { get; set; } = null!;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public TaskStatus Status { get; set; } = TaskStatus.New;

        public DateTime? DueDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? CompletedAt { get; set; }

        public decimal? EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // ── Deadline Notification Flags ───────────────────────
        /// <summary>Set to true by the deadline job after the 48-hour warning is sent.
        /// Prevents duplicate "due soon" alerts on subsequent job cycles.</summary>
        public bool AlreadyNotifiedDueSoon { get; set; } = false;

        /// <summary>Set to true by the deadline job after the overdue alert is sent.
        /// Prevents duplicate "overdue" alerts on subsequent job cycles.</summary>
        public bool AlreadyNotifiedOverdue { get; set; } = false;

        public ICollection<TaskComment> Comments { get; set; } = new HashSet<TaskComment>();
    }
}
