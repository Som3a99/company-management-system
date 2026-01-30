using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.DAL.Data.Configurations
{
    public class EmployeeConfigurations : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("Employees");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Id)
                .UseIdentityColumn(1000, 1);

            builder.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(15);

            builder.Property(e => e.Position)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.HireDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(e => e.Salary)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("Cast(GETUTCDATE() as Date)");

            builder.HasOne(e => e.ManagedDepartment)
                   .WithOne(d => d.Manager)
                   .HasForeignKey<Department>(d => d.ManagerId);

            builder.HasIndex(e => e.IsDeleted);

            // Index on ProjectId for query performance
            builder.HasIndex(e => e.ProjectId);

            builder.HasIndex(e => new { e.IsActive, e.IsDeleted });

            builder.HasIndex(e => e.DepartmentId);

            builder.HasIndex(e => e.Email)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_Employees_Email_Unique")
                .HasAnnotation("Relational:Collation", "Latin1_General_CI_AS"); // Case-insensitive
        }
    }
}
