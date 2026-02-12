using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.DAL.Data.Configurations
{
    public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
    {
        public void Configure(EntityTypeBuilder<TaskItem> builder)
        {
            builder.ToTable("TaskItems");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id)
                .UseIdentityColumn(1000, 1);

            builder.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(t => t.Description)
                .HasMaxLength(2000);

            builder.Property(t => t.Priority)
                .IsRequired()
                .HasDefaultValue(TaskPriority.Medium)
                .HasSentinel(TaskPriority.None);

            builder.Property(t => t.Status)
                .IsRequired()
                .HasDefaultValue(TaskStatus.New)
                .HasSentinel(TaskStatus.None);

            builder.Property(t => t.EstimatedHours)
                .HasColumnType("decimal(9,2)");

            builder.Property(t => t.ActualHours)
                .HasColumnType("decimal(9,2)")
                .HasDefaultValue(0m);

            builder.Property(t => t.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(t => t.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(t => t.RowVersion)
                .IsRowVersion();

            builder.HasOne(t => t.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.AssignedToEmployee)
                .WithMany(e => e.AssignedTasks)
                .HasForeignKey(t => t.AssignedToEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.CreatedByUser)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(t => t.Comments)
                .WithOne(c => c.Task)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(t => t.ProjectId);
            builder.HasIndex(t => t.AssignedToEmployeeId);
            builder.HasIndex(t => t.Status);
            builder.HasIndex(t => t.DueDate);
            builder.HasIndex(t => new { t.ProjectId, t.Status, t.Priority });

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_TaskItem_Hours_NonNegative", "[ActualHours] >= 0 AND ([EstimatedHours] IS NULL OR [EstimatedHours] >= 0)");
            });
        }
    }
}
