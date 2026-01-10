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
        }
    }
}
