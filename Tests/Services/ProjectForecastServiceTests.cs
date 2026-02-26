using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using ERP.BLL.Services;
using ERP.DAL.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Infrastructure;
using TaskStatus = ERP.DAL.Models.TaskStatus;
using ProjectStatus = ERP.DAL.Models.ProjectStatus;

namespace Tests.Services
{
    public class ProjectForecastServiceTests
    {
        private readonly Mock<ICacheService> _cacheMock;
        private readonly Mock<ILogger<ProjectForecastService>> _loggerMock;

        public ProjectForecastServiceTests()
        {
            _cacheMock = new Mock<ICacheService>();
            _loggerMock = new Mock<ILogger<ProjectForecastService>>();

            // Configure cache to always execute the factory
            _cacheMock.Setup(c => c.GetOrCreateNullableAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<ProjectForecastResult?>>>(),
                    It.IsAny<TimeSpan>()))
                .Returns<string, Func<Task<ProjectForecastResult?>>, TimeSpan>((_, factory, _) => factory());
        }

        private ProjectForecastService CreateService(string dbName)
        {
            var context = TestDbContextFactory.Create(dbName);
            return new ProjectForecastService(context, _cacheMock.Object, _loggerMock.Object);
        }

        #region DetermineStatus Tests

        [Theory]
        [InlineData(0, 1.0, 5, "2025-12-31", "On Track")]
        [InlineData(3, 1.0, 5, "2025-12-31", "At Risk")]
        [InlineData(5, 1.0, 5, "2025-12-31", "At Risk")]
        [InlineData(10, 1.0, 5, "2025-12-31", "Behind")]
        [InlineData(0, 0.0, 5, "2025-12-31", "Behind")]
        [InlineData(0, 1.0, 0, "2025-12-31", "On Track")]
        [InlineData(0, 1.0, 5, null, "On Track")]
        public void DetermineStatus_ReturnsExpected(int daysBehind, double velocity, int remaining, string? endDateStr, string expected)
        {
            DateTime? endDate = endDateStr != null ? DateTime.Parse(endDateStr) : null;
            var result = ProjectForecastService.DetermineStatus(daysBehind, velocity, remaining, endDate);
            result.Should().Be(expected);
        }

        #endregion

        #region ForecastAsync Tests

        [Fact]
        public async Task ForecastAsync_NonExistentProject_ReturnsNull()
        {
            var service = CreateService(nameof(ForecastAsync_NonExistentProject_ReturnsNull));
            var result = await service.ForecastAsync(999);
            result.Should().BeNull();
        }

        [Fact]
        public async Task ForecastAsync_CompletedProject_ReturnsNull()
        {
            var dbName = nameof(ForecastAsync_CompletedProject_ReturnsNull);
            var context = TestDbContextFactory.Create(dbName);

            var dept = new Department { Id = 1, DepartmentCode = "D1", DepartmentName = "Test" };
            context.Departments.Add(dept);

            context.Projects.Add(new Project
            {
                Id = 1,
                ProjectCode = "P1",
                ProjectName = "Done Project",
                StartDate = DateTime.UtcNow.AddMonths(-3),
                Status = ProjectStatus.Completed,
                DepartmentId = 1
            });
            await context.SaveChangesAsync();

            var service = new ProjectForecastService(context, _cacheMock.Object, _loggerMock.Object);
            var result = await service.ForecastAsync(1);
            result.Should().BeNull();
        }

        [Fact]
        public async Task ForecastAsync_NoTasks_ReturnsNull()
        {
            var dbName = nameof(ForecastAsync_NoTasks_ReturnsNull);
            var context = TestDbContextFactory.Create(dbName);

            var dept = new Department { Id = 1, DepartmentCode = "D1", DepartmentName = "Test" };
            context.Departments.Add(dept);

            context.Projects.Add(new Project
            {
                Id = 1,
                ProjectCode = "P1",
                ProjectName = "Empty Project",
                StartDate = DateTime.UtcNow.AddMonths(-1),
                Status = ProjectStatus.InProgress,
                DepartmentId = 1
            });
            await context.SaveChangesAsync();

            var service = new ProjectForecastService(context, _cacheMock.Object, _loggerMock.Object);
            var result = await service.ForecastAsync(1);
            result.Should().BeNull();
        }

        [Fact]
        public async Task ForecastAsync_ZeroVelocity_Returns180DaysOut()
        {
            var dbName = nameof(ForecastAsync_ZeroVelocity_Returns180DaysOut);
            var context = TestDbContextFactory.Create(dbName);

            var dept = new Department { Id = 1, DepartmentCode = "D1", DepartmentName = "Test" };
            context.Departments.Add(dept);

            var project = new Project
            {
                Id = 1,
                ProjectCode = "P1",
                ProjectName = "Stuck Project",
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddMonths(1),
                Status = ProjectStatus.InProgress,
                DepartmentId = 1
            };
            context.Projects.Add(project);

            // Add tasks that are not completed
            context.TaskItems.Add(new TaskItem
            {
                Id = 1,
                Title = "Task 1",
                Status = TaskStatus.InProgress,
                ProjectId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                CreatedByUserId = "test-user"
            });
            await context.SaveChangesAsync();

            var service = new ProjectForecastService(context, _cacheMock.Object, _loggerMock.Object);
            var result = await service.ForecastAsync(1);

            result.Should().NotBeNull();
            result!.Velocity.Should().Be(0);
            result.RemainingTasks.Should().Be(1);
            result.CompletedTasksLast30Days.Should().Be(0);
            result.EstimatedCompletionDate.Should().BeAfter(DateTime.UtcNow.AddDays(170));
        }

        [Fact]
        public async Task ForecastAsync_ActiveProject_CalculatesVelocity()
        {
            var dbName = nameof(ForecastAsync_ActiveProject_CalculatesVelocity);
            var context = TestDbContextFactory.Create(dbName);

            var dept = new Department { Id = 1, DepartmentCode = "D1", DepartmentName = "Test" };
            context.Departments.Add(dept);

            var project = new Project
            {
                Id = 1,
                ProjectCode = "P1",
                ProjectName = "Active Project",
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddMonths(3),
                Status = ProjectStatus.InProgress,
                DepartmentId = 1
            };
            context.Projects.Add(project);

            // 10 completed in last 30 days => velocity = 10/30 ≈ 0.33/day
            for (int i = 1; i <= 10; i++)
            {
                context.TaskItems.Add(new TaskItem
                {
                    Id = i,
                    Title = $"Completed Task {i}",
                    Status = TaskStatus.Completed,
                    CompletedAt = DateTime.UtcNow.AddDays(-i),
                    ProjectId = 1,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    CreatedByUserId = "test-user"
                });
            }

            // 5 remaining tasks
            for (int i = 11; i <= 15; i++)
            {
                context.TaskItems.Add(new TaskItem
                {
                    Id = i,
                    Title = $"Remaining Task {i}",
                    Status = TaskStatus.InProgress,
                    ProjectId = 1,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    CreatedByUserId = "test-user"
                });
            }
            await context.SaveChangesAsync();

            var service = new ProjectForecastService(context, _cacheMock.Object, _loggerMock.Object);
            var result = await service.ForecastAsync(1);

            result.Should().NotBeNull();
            result!.Velocity.Should().BeApproximately(0.33, 0.01);
            result.RemainingTasks.Should().Be(5);
            result.CompletedTasksLast30Days.Should().Be(10);
            result.Status.Should().Be("On Track");
        }

        [Fact]
        public async Task ForecastAsync_BehindSchedule_CorrectStatus()
        {
            var dbName = nameof(ForecastAsync_BehindSchedule_CorrectStatus);
            var context = TestDbContextFactory.Create(dbName);

            var dept = new Department { Id = 1, DepartmentCode = "D1", DepartmentName = "Test" };
            context.Departments.Add(dept);

            // Project ending in 2 days
            var project = new Project
            {
                Id = 1,
                ProjectCode = "P1",
                ProjectName = "Tight Deadline",
                StartDate = DateTime.UtcNow.AddMonths(-3),
                EndDate = DateTime.UtcNow.AddDays(2),
                Status = ProjectStatus.InProgress,
                DepartmentId = 1
            };
            context.Projects.Add(project);

            // Only 1 completed in 30 days => velocity = 1/30 ≈ 0.033
            context.TaskItems.Add(new TaskItem
            {
                Id = 1,
                Title = "Done Task",
                Status = TaskStatus.Completed,
                CompletedAt = DateTime.UtcNow.AddDays(-5),
                ProjectId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                CreatedByUserId = "test-user"
            });

            // 20 remaining tasks => 20 / 0.033 = ~600 days
            for (int i = 2; i <= 21; i++)
            {
                context.TaskItems.Add(new TaskItem
                {
                    Id = i,
                    Title = $"Task {i}",
                    Status = TaskStatus.New,
                    ProjectId = 1,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    CreatedByUserId = "test-user"
                });
            }
            await context.SaveChangesAsync();

            var service = new ProjectForecastService(context, _cacheMock.Object, _loggerMock.Object);
            var result = await service.ForecastAsync(1);

            result.Should().NotBeNull();
            result!.Status.Should().Be("Behind");
            result.DaysBehindSchedule.Should().BeGreaterThan(5);
        }

        #endregion
    }
}
