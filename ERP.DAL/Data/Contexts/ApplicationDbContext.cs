using ERP.DAL.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ERP.DAL.Data.Contexts
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Project> Projects { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; } = null!;
        public DbSet<ProjectEmployee> ProjectEmployees { get; set; } = null!;
        public DbSet<TaskItem> TaskItems { get; set; } = null!;
        public DbSet<TaskComment> TaskComments { get; set; } = null!;
        public DbSet<ReportJob> ReportJobs { get; set; } = null!;
        public DbSet<ReportPreset> ReportPresets { get; set; } = null!;
        public DbSet<SeedHistory> SeedHistories { get; set; } = null!;
        public DbSet<AppNotification> Notifications { get; set; } = null!;
        public DbSet<NotificationPreference> NotificationPreferences { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // This configures Identity tables
            base.OnModelCreating(modelBuilder);

            // Configure Employee <-> ApplicationUser relationship
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.ApplicationUser)
                .WithOne(u => u.Employee)
                .HasForeignKey<Employee>(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull); // Keep employee if user deleted

            // Configure ApplicationUser <-> Employee reverse
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Employee)
                .WithOne(e => e.ApplicationUser)
                .HasForeignKey<ApplicationUser>(u => u.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull); // Keep user if employee deleted

            // Configure AuditLog
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLogs");
                entity.HasIndex(a => a.Timestamp);
                entity.HasIndex(a => a.UserId);
                entity.HasIndex(a => new { a.ResourceType, a.ResourceId });
            });

            // Configure PasswordResetRequest
            modelBuilder.Entity<PasswordResetRequest>(entity =>
            {
                entity.ToTable("PasswordResetRequests");
                entity.HasIndex(p => p.TicketNumber)
                    .IsUnique();
                entity.HasIndex(p => p.Status);
                entity.HasIndex(p => new { p.UserId, p.Status });
                entity.HasIndex(p => p.ExpiresAt);
            });

            modelBuilder.Entity<ReportJob>(entity =>
            {
                entity.ToTable("ReportJobs");
                entity.HasIndex(r => new { r.Status, r.RequestedAtUtc });
                entity.HasIndex(r => r.RequestedByUserId);
            });

            modelBuilder.Entity<ReportPreset>(entity =>
            {
                entity.ToTable("ReportPresets");
                entity.Property(r => r.Name).HasMaxLength(120).IsRequired();
                entity.HasIndex(r => new { r.UserId, r.ReportType, r.Name }).IsUnique();
            });

            // Configure SeedHistory
            modelBuilder.Entity<SeedHistory>(entity =>
            {
                entity.ToTable("SeedHistories");
                entity.Property(s => s.SeedVersion).HasMaxLength(50).IsRequired();
                entity.Property(s => s.Environment).HasMaxLength(50).IsRequired();
                entity.HasIndex(s => new { s.SeedVersion, s.Environment });
            });

            // Configure AppNotification
            modelBuilder.Entity<AppNotification>(b =>
            {
                b.HasKey(n => n.Id);
                b.Property(n => n.Title).HasMaxLength(120).IsRequired();
                b.Property(n => n.Message).HasMaxLength(500).IsRequired();
                b.Property(n => n.LinkUrl).HasMaxLength(300);

                // Indexes
                b.HasIndex(n => n.RecipientUserId);
                b.HasIndex(n => new { n.RecipientUserId, n.IsRead });
                b.HasIndex(n => new { n.RecipientUserId, n.IsArchived });
                b.HasIndex(n => n.Severity);
                b.HasIndex(n => n.CreatedAt);

                b.HasOne(n => n.RecipientUser)
                    .WithMany()
                    .HasForeignKey(n => n.RecipientUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // NotificationPreference configuration
            modelBuilder.Entity<NotificationPreference>(b =>
            {
                b.HasKey(p => p.Id);
                b.HasIndex(p => p.UserId).IsUnique();
                b.HasOne(p => p.User)
                    .WithMany()
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Apply existing configurations
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            
            // Global query filters for soft delete
            modelBuilder.Entity<Employee>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Department>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<Project>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<ProjectEmployee>().HasQueryFilter(pe => !pe.Employee.IsDeleted && !pe.Project.IsDeleted);

            // Global query filter for archived notifications
            modelBuilder.Entity<AppNotification>().HasQueryFilter(n => !n.IsArchived);
        }
    }
}
