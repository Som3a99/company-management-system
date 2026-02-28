using ERP.BLL.Common;
using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using ERP.BLL.Services;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tests.Infrastructure;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace Tests.Services
{
    /// <summary>
    /// Reliability tests: verify that failures in non-critical subsystems
    /// do not cascade into system crashes (HTTP 500).
    /// </summary>
    public class ReliabilityTests
    {
        #region Task Creation — DB failure returns error result instead of throwing

        [Fact]
        public async Task CreateTaskAsync_DbSaveFails_ReturnsInvalidInsteadOfThrowing()
        {
            using var db = TestDbContextFactory.Create();

            var projectRepo = new Mock<IProjectRepository>();
            projectRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(
                new Project { Id = 1, ProjectManagerId = 10, DepartmentId = 1, ProjectCode = "P1", ProjectName = "P1" });

            var employeeRepo = new Mock<IEmployeeRepository>();
            employeeRepo.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(
                new Employee { Id = 5, FirstName = "A", LastName = "B", Email = "a@x.com", PhoneNumber = "1234567", Position = "Dev", ImageUrl = "x", Gender = Gender.Male });

            var taskRepo = new Mock<ITaskRepository>();
            taskRepo.Setup(x => x.IsEmployeeAssignedToProjectAsync(5, 1)).ReturnsAsync(true);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(x => x.CompleteAsync())
                .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("FK constraint violation"));

            var accessor = TestHttpContextFactory.CreateAccessor(
                claims: new[] { new System.Security.Claims.Claim("EmployeeId", "10") },
                roles: new[] { "ProjectManager" });

            var service = new TaskService(
                taskRepo.Object,
                projectRepo.Object,
                employeeRepo.Object,
                unitOfWork.Object,
                accessor,
                db,
                Mock.Of<ICacheService>(),
                Mock.Of<ILogger<TaskService>>(),
                Mock.Of<INotificationService>(),
                Mock.Of<ITaskRiskService>());

            var req = new CreateTaskRequest("Test Task", "Desc", 1, 5, TaskPriority.Medium, null, null, 4m);
            var result = await service.CreateTaskAsync(req, "user-1");

            result.Succeeded.Should().BeFalse("DB failure should return a safe error, not throw");
            result.Error.Should().Contain("Unable to save task");
        }

        #endregion

        #region Digest — Anomaly service failure does not crash digest

        [Fact]
        public async Task PrepareDigestAsync_AnomalyServiceThrows_ReturnsDigestWithZeroAnomalies()
        {
            using var db = TestDbContextFactory.Create();

            var anomalyService = new Mock<IAuditAnomalyService>();
            anomalyService.Setup(x => x.DetectAnomaliesAsync(It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("DB connection lost"));

            var teamHealthService = new Mock<ITeamHealthService>();
            teamHealthService.Setup(x => x.CalculateAsync())
                .ReturnsAsync(new TeamHealthScoreResult { Score = 85, Status = "Healthy" });

            var taskRiskService = new Mock<ITaskRiskService>();
            taskRiskService.Setup(x => x.CalculateRisk(It.IsAny<TaskItem>()))
                .Returns(new TaskRiskResult { Score = 0, Level = "Low", Reason = "OK" });

            var cache = new MemoryCache(new MemoryCacheOptions());
            var logger = NullLogger<ExecutiveDigestService>.Instance;

            var service = new ExecutiveDigestService(
                db, taskRiskService.Object, teamHealthService.Object,
                anomalyService.Object, cache, logger);

            var digest = await service.PrepareDigestAsync();

            digest.Should().NotBeNull();
            digest.AnomaliesDetected.Should().Be(0, "anomaly failure should default to 0");
        }

        #endregion

        #region Digest — Team health failure does not crash digest

        [Fact]
        public async Task PrepareDigestAsync_TeamHealthThrows_ReturnsDigestWithDefaultHealth()
        {
            using var db = TestDbContextFactory.Create();

            var anomalyService = new Mock<IAuditAnomalyService>();
            anomalyService.Setup(x => x.DetectAnomaliesAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<AuditAnomalyFlag>());

            var teamHealthService = new Mock<ITeamHealthService>();
            teamHealthService.Setup(x => x.CalculateAsync())
                .ThrowsAsync(new TimeoutException("Service timeout"));

            var taskRiskService = new Mock<ITaskRiskService>();
            taskRiskService.Setup(x => x.CalculateRisk(It.IsAny<TaskItem>()))
                .Returns(new TaskRiskResult { Score = 0, Level = "Low", Reason = "OK" });

            var cache = new MemoryCache(new MemoryCacheOptions());
            var logger = NullLogger<ExecutiveDigestService>.Instance;

            var service = new ExecutiveDigestService(
                db, taskRiskService.Object, teamHealthService.Object,
                anomalyService.Object, cache, logger);

            var digest = await service.PrepareDigestAsync();

            digest.Should().NotBeNull();
            digest.TeamHealth.Should().NotBeNull();
            digest.TeamHealth.Status.Should().Be("Unavailable", "health failure should have safe fallback");
        }

        #endregion

        #region Anomaly Detection — Empty dataset handled gracefully

        [Fact]
        public async Task DetectAnomaliesAsync_EmptyAuditLogs_ReturnsEmptyList()
        {
            using var db = TestDbContextFactory.Create();

            var cache = new MemoryCache(new MemoryCacheOptions());
            var logger = NullLogger<AuditAnomalyService>.Instance;

            var service = new AuditAnomalyService(db, cache, logger);

            var result = await service.DetectAnomaliesAsync(24);

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region MemoryCache Size — entries are properly cached

        [Fact]
        public async Task GetOrCreateAsync_WithSizeLimit_ShouldCacheEntries()
        {
            var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });

            var callCount = 0;
            var firstResult = await cache.GetOrCreateAsync("test-key", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.Size = 1; // This is the fix — without this, entries are discarded
                callCount++;
                return await Task.FromResult(new List<string> { "item" });
            });

            var secondResult = await cache.GetOrCreateAsync("test-key", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.Size = 1;
                callCount++;
                return await Task.FromResult(new List<string> { "item2" });
            });

            callCount.Should().Be(1, "factory should only run once — second call should use cached value");
            firstResult.Should().BeEquivalentTo(secondResult);
        }

        [Fact]
        public async Task GetOrCreateAsync_WithoutSize_ThrowsInvalidOperationException()
        {
            // THIS TEST DOCUMENTS THE ROOT CAUSE:
            // When MemoryCache has SizeLimit set and GetOrCreateAsync is called
            // WITHOUT setting entry.Size, it THROWS InvalidOperationException.
            // This was the exact exception crashing the Digest/Anomaly endpoints.
            var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });

            var act = () => cache.GetOrCreateAsync("test-key-no-size", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                // BUG: No entry.Size set — this throws
                return await Task.FromResult("value");
            });

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Size*SizeLimit*");
        }

        #endregion
    }
}
