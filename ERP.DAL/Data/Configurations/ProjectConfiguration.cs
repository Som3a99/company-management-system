using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.DAL.Data.Configurations
{
    public class ProjectConfiguration : IEntityTypeConfiguration<Project>
    {
        public void Configure(EntityTypeBuilder<Project> builder)
        {
            builder.ToTable("Projects");

            builder.Property(p => p.Id)
                .UseIdentityColumn(1000, 1);

            builder.HasKey(p => p.Id);

            // Project Code configuration with validation
            builder.Property(p => p.ProjectCode)
                .IsRequired()
                .HasMaxLength(50)
                .UseCollation("Latin1_General_CI_AS");

            // Check constraint for ProjectCode format: PRJ-YYYY-XXX (e.g., PRJ-2026-001)
            builder.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Project_ProjectCode_Format",
                    "ProjectCode LIKE 'PRJ-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9]'"
                );
            });

            builder.Property(p => p.ProjectName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(p => p.Description)
                .HasMaxLength(1000);

            builder.Property(p => p.StartDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(p => p.EndDate)
                .HasColumnType("date");

            builder.Property(p => p.Budget)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            // Set default value to Planning and sentinel to None (0)
            builder.Property(p => p.Status)
                .IsRequired()
                .HasDefaultValue(ProjectStatus.Planning)
                .HasSentinel(ProjectStatus.None);  // Explicitly set sentinel to None

            builder.Property(p => p.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(p => p.CreatedAt)
                .HasDefaultValueSql("Cast(GETUTCDATE() as Date)");

            // Department relationship (One-to-Many)
            builder.HasOne(p => p.Department)
                .WithMany(d => d.Projects)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Project Manager relationship (One-to-One)
            builder.HasOne(p => p.ProjectManager)
                .WithOne(e => e.ManagedProject)
                .HasForeignKey<Project>(p => p.ProjectManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(p => p.IsDeleted);

            builder.HasIndex(p => p.DepartmentId);

            // Unique index on ProjectCode (only for non-deleted)
            builder.HasIndex(p => p.ProjectCode)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_Projects_ProjectCode_Unique");

            // Unique index on ProjectManagerId (only for non-deleted and non-null)
            builder.HasIndex(p => p.ProjectManagerId)
                .IsUnique()
                .HasFilter("[ProjectManagerId] IS NOT NULL AND [IsDeleted] = 0")
                .HasDatabaseName("IX_Projects_ProjectManagerId_Unique");

            // Composite index for status and department queries
            builder.HasIndex(p => new { p.Status, p.DepartmentId, p.IsDeleted });
        }
    }
}
