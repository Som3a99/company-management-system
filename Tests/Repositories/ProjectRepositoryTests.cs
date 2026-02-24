using ERP.BLL.Repositories;
using ERP.DAL.Models;
using FluentAssertions;
using System.Security.Claims;
using Tests.Infrastructure;

namespace Tests.Repositories
{
    public class ProjectRepositoryTests
    {
        [Fact]
        public async Task GetAllAsync_ShouldReturnScopedProjects_ForManagedDepartment()
        {
            using var db = TestDbContextFactory.Create();
            db.Projects.AddRange(
                new Project { Id = 1, ProjectCode = "PRJ_001", ProjectName = "One", DepartmentId = 1, StartDate = DateTime.UtcNow },
                new Project { Id = 2, ProjectCode = "PRJ_002", ProjectName = "Two", DepartmentId = 2, StartDate = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var accessor = TestHttpContextFactory.CreateAccessor(
                claims: new[] { new Claim("ManagedDepartmentId", "2") },
                roles: new[] { "DepartmentManager" });

            var repo = new ProjectRepository(db, accessor, new PassthroughCacheService());

            var items = (await repo.GetAllAsync()).ToList();

            items.Should().ContainSingle();
            items[0].DepartmentId.Should().Be(2);
        }

        [Fact]
        public async Task ProjectCodeExistsAsync_ShouldBeCaseInsensitive()
        {
            using var db = TestDbContextFactory.Create();
            db.Projects.Add(new Project { Id = 1, ProjectCode = "PRJ_ABC", ProjectName = "One", DepartmentId = 1, StartDate = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var repo = new ProjectRepository(db, TestHttpContextFactory.CreateAccessor(roles: new[] { "CEO" }), new PassthroughCacheService());

            var exists = await repo.ProjectCodeExistsAsync("prj_abc");

            exists.Should().BeTrue();
        }
    }
}
