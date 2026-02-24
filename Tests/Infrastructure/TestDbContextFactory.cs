using ERP.DAL.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;

namespace Tests.Infrastructure
{
    internal static class TestDbContextFactory
    {
        public static ApplicationDbContext Create(string? dbName = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString("N"))
                .Options;

            return new ApplicationDbContext(options);
        }
    }
}
