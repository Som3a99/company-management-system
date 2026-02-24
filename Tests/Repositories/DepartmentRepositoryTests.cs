using ERP.BLL.Repositories;
using ERP.DAL.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tests.Infrastructure;

namespace Tests.Repositories
{
    public class DepartmentRepositoryTests
    {
        [Fact]
        public async Task GetAllAsync_ShouldReturnOnlyManagedDepartment_ForDepartmentManager()
        {
            using var db = TestDbContextFactory.Create();
            db.Departments.AddRange(
                new Department { Id = 1, DepartmentCode = "DEP_001", DepartmentName = "Engineering" },
                new Department { Id = 2, DepartmentCode = "DEP_002", DepartmentName = "Finance" });
            await db.SaveChangesAsync();

            var accessor = TestHttpContextFactory.CreateAccessor(
                claims: new[] { new Claim("ManagedDepartmentId", "2") },
                roles: new[] { "DepartmentManager" });

            var repo = new DepartmentRepository(db, accessor, new PassthroughCacheService());

            var items = (await repo.GetAllAsync()).ToList();

            items.Should().ContainSingle();
            items[0].Id.Should().Be(2);
        }

        [Fact]
        public async Task DeleteAsync_ShouldSoftDeleteDepartment()
        {
            using var db = TestDbContextFactory.Create();
            db.Departments.Add(new Department { Id = 1, DepartmentCode = "DEP_001", DepartmentName = "Engineering" });
            await db.SaveChangesAsync();

            var repo = new DepartmentRepository(db, TestHttpContextFactory.CreateAccessor(roles: new[] { "CEO" }), new PassthroughCacheService());
            await repo.DeleteAsync(1);
            await db.SaveChangesAsync();

            db.Departments.Count().Should().Be(0);
            db.Departments.IgnoreQueryFilters().Single(d => d.Id == 1).IsDeleted.Should().BeTrue();
        }
    }
}
