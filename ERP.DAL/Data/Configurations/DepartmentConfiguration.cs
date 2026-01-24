using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.DAL.Data.Configurations
{
    public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
    {
        public void Configure(EntityTypeBuilder<Department> builder)
        {
            builder.ToTable("Departments");
            builder.Property(d => d.Id)
                .UseIdentityColumn(100, 1);
            builder.HasKey(d => d.Id);

            // Regex pattern for DepartmentCode: 3 uppercase letters followed by underscore _ and 3 digits (e.g., ABC_123)
            builder.Property(d => d.DepartmentCode)
                .IsRequired()
                .HasMaxLength(50);

            builder.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Department_DepartmentCode_Format",
                    "DepartmentCode LIKE '[A-Z][A-Z][A-Z]_[0-9][0-9][0-9]'"
                );
            });

            builder.Property(d => d.DepartmentName)
                .IsRequired()
                .HasMaxLength(100);

            // Setting default value for CreatedAt to current timestamp in SQL Server DateOnly format
            builder.Property(d => d.CreatedAt)
                .HasDefaultValueSql("Cast(GETUTCDATE() as Date)");

            // Configure Manager relationship
            builder.HasOne(d => d.Manager)
                .WithOne(e => e.ManagedDepartment)
                .HasForeignKey<Department>(d => d.ManagerId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

            builder.HasMany(d => d.Employees)
                .WithOne(e => e.Department)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent orphaning employees

            /// <summary>
            /// IsDeleted configuration with default value
            /// </summary>
            builder.Property(d => d.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            /// <summary>
            /// Index on IsDeleted for query performance
            /// </summary>
            builder.HasIndex(d => d.IsDeleted);

            /// <summary>
            /// Unique index on DepartmentCode (only for non-deleted)
            /// This allows same code to be reused after soft delete
            /// </summary>
            builder.HasIndex(d => d.DepartmentCode)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

        }
    }
}
