using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tests.Infrastructure;

namespace Tests.Services
{
    public class CacheWarmupHostedServiceTests
    {
        [Fact]
        public async Task StartAsync_ShouldPopulateDepartmentsAndDashboardCaches()
        {
            using var db = TestDbContextFactory.Create();
            db.Departments.AddRange(
                new Department { Id = 1, DepartmentCode = "DEP_001", DepartmentName = "Engineering", CreatedAt = DateTime.UtcNow },
                new Department { Id = 2, DepartmentCode = "DEP_002", DepartmentName = "Finance", CreatedAt = DateTime.UtcNow });
            db.PasswordResetRequests.AddRange(
                new PasswordResetRequest { Id = 1, UserId = "u1", UserEmail = "u1@test.local", TicketNumber = "RST-001", Status = ResetStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(-5), RequestedAt = DateTime.UtcNow },
                new PasswordResetRequest { Id = 2, UserId = "u2", UserEmail = "u2@test.local", TicketNumber = "RST-002", Status = ResetStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(30), RequestedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var services = new ServiceCollection();
            services.AddSingleton(db);
            services.AddSingleton<ICacheService, PassthroughCacheService>();
            services.AddSingleton(CreateMockUserManagerWithQueryableUsers(Array.Empty<ApplicationUser>()).Object);
            services.AddLogging();

            var provider = services.BuildServiceProvider();
            var sut = new CacheWarmupHostedService(provider.GetRequiredService<IServiceScopeFactory>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CacheWarmupHostedService>.Instance);

            await sut.StartAsync(CancellationToken.None);

            var cache = (PassthroughCacheService)provider.GetRequiredService<ICacheService>();
            var departments = await cache.GetAsync<List<Department>>(CacheKeys.DepartmentsAll);
            var stats = await cache.GetAsync<ITAdminDashboardStats>(CacheKeys.ItAdminDashboard);

            departments.Should().NotBeNull();
            departments!.Should().HaveCount(2);
            stats.Should().NotBeNull();
            stats!.PendingResets.Should().Be(2);
            stats.ExpiredResets.Should().Be(1);
            stats.LockedAccounts.Should().Be(0);
        }

        private static Mock<UserManager<ApplicationUser>> CreateMockUserManagerWithQueryableUsers(IReadOnlyList<ApplicationUser> users)
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var manager = new Mock<UserManager<ApplicationUser>>(
                store.Object,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!);

            manager.SetupGet(x => x.Users).Returns(users.AsQueryable());
            return manager;
        }
    }
}
