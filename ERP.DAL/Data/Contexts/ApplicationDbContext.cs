using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace ERP.DAL.Data.Contexts
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Project> Projects { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Global query filters for soft delete
            modelBuilder.Entity<Employee>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Department>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<Project>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
