using ERP.BLL.Repositories;
using ERP.DAL.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tests.Infrastructure;

namespace Tests.Repositories
{
    public class EmployeeRepositoryTests
    {
        [Fact]
        public async Task GetAllAsync_ShouldReturnOnlyDepartmentPeers_ForRegularEmployee()
        {
            using var db = TestDbContextFactory.Create();
            db.Employees.AddRange(
                MakeEmployee(1, 1),
                MakeEmployee(2, 1),
                MakeEmployee(3, 2));
            await db.SaveChangesAsync();

            var accessor = TestHttpContextFactory.CreateAccessor(
                claims: new[] { new Claim("DepartmentId", "1") },
                roles: new[] { "Employee" });

            var repo = new EmployeeRepository(db, accessor);

            var items = (await repo.GetAllAsync()).ToList();

            items.Should().HaveCount(2);
            items.Should().OnlyContain(x => x.DepartmentId == 1);
        }

        [Fact]
        public async Task DeleteAsync_ShouldSoftDeleteEmployee()
        {
            using var db = TestDbContextFactory.Create();
            db.Employees.Add(MakeEmployee(7, 1));
            await db.SaveChangesAsync();

            var repo = new EmployeeRepository(db, TestHttpContextFactory.CreateAccessor(roles: new[] { "CEO" }));

            await repo.DeleteAsync(7);
            await db.SaveChangesAsync();

            db.Employees.Count().Should().Be(0);
            db.Employees.IgnoreQueryFilters().Single(e => e.Id == 7).IsDeleted.Should().BeTrue();
        }

        private static Employee MakeEmployee(int id, int departmentId) => new()
        {
            Id = id,
            FirstName = $"Emp{id}",
            LastName = "User",
            Email = $"emp{id}@test.local",
            PhoneNumber = "1234567",
            Position = "Dev",
            ImageUrl = "img",
            Gender = Gender.Male,
            DepartmentId = departmentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
