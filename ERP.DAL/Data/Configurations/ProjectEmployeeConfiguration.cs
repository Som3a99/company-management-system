using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.DAL.Data.Configurations
{
    public class ProjectEmployeeConfiguration : IEntityTypeConfiguration<ProjectEmployee>
    {
        public void Configure(EntityTypeBuilder<ProjectEmployee> builder)
        {
            builder.ToTable("ProjectEmployees");

            builder.HasKey(pe => new { pe.ProjectId, pe.EmployeeId });

            builder.Property(pe => pe.AssignedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(pe => pe.AssignedBy)
                .IsRequired()
                .HasMaxLength(450);

            builder.HasOne(pe => pe.Project)
                .WithMany(p => p.ProjectEmployees)
                .HasForeignKey(pe => pe.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(pe => pe.Employee)
                .WithMany(e => e.ProjectEmployees)
                .HasForeignKey(pe => pe.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(pe => new { pe.ProjectId, pe.EmployeeId })
                .IsUnique()
                .HasDatabaseName("IX_ProjectEmployees_ProjectId_EmployeeId_Unique");

            builder.HasIndex(pe => pe.EmployeeId);
        }
    }
}
