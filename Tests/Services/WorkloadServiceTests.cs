using ERP.BLL.DTOs;
using ERP.BLL.Services;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Infrastructure;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace Tests.Services
{
    public class WorkloadServiceTests
    {
        private WorkloadService CreateService(ApplicationDbContext db)
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var logger = new Mock<ILogger<WorkloadService>>();
            return new WorkloadService(db, cache, logger.Object);
        }

        #region Basic Workload Calculation

        [Fact]
        public async Task GetWorkloadAsync_ReturnsCorrectTaskCount()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            SeedTasks(db, employeeId: 1, count: 3, estimatedHours: 10m, actualHours: 2m);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var results = await service.GetWorkloadAsync(projectId: 1);

            results.Should().HaveCount(1);
            results[0].ActiveTasks.Should().Be(3);
            results[0].EmployeeId.Should().Be(1);
        }

        [Fact]
        public async Task GetWorkloadAsync_CalculatesRemainingHours()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            // 2 tasks: (10-2) + (10-2) = 16 remaining hours
            SeedTasks(db, employeeId: 1, count: 2, estimatedHours: 10m, actualHours: 2m);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var results = await service.GetWorkloadAsync(projectId: 1);

            results[0].RemainingHours.Should().Be(16);
        }

        #endregion

        #region Load Score Calculation

        [Fact]
        public async Task GetWorkloadAsync_CalculatesLoadScore()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            // 4 tasks, 8h remaining each = 32h total
            // Score = (4 * 5) + (32 / 2) = 20 + 16 = 36
            SeedTasks(db, employeeId: 1, count: 4, estimatedHours: 10m, actualHours: 2m);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var results = await service.GetWorkloadAsync(projectId: 1);

            results[0].LoadScore.Should().Be(36);
            results[0].Label.Should().Be("Moderate");
        }

        [Fact]
        public async Task GetWorkloadAsync_HeavyLoad_LabelsCorrectly()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            // 9 tasks, 40h remaining each = 360h -> score = (9*5) + (360/2) = 45 + 180 = clamped to 100
            SeedTasks(db, employeeId: 1, count: 9, estimatedHours: 50m, actualHours: 10m);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var results = await service.GetWorkloadAsync(projectId: 1);

            results[0].LoadScore.Should().Be(100);
            results[0].Label.Should().Be("Heavy");
        }

        [Fact]
        public async Task GetWorkloadAsync_LightLoad_LabelsAvailable()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            // 1 task, 3h remaining => score = (1*5) + (3/2) = 5 + 1 = 6
            SeedTasks(db, employeeId: 1, count: 1, estimatedHours: 5m, actualHours: 2m);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var results = await service.GetWorkloadAsync(projectId: 1);

            results[0].LoadScore.Should().BeLessThanOrEqualTo(30);
            results[0].Label.Should().Be("Available");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task GetWorkloadAsync_NoTasks_ReturnsEmpty()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var results = await service.GetWorkloadAsync(projectId: 1);

            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetWorkloadAsync_CompletedTasksExcluded()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            // Add 2 completed tasks — should be excluded
            db.TaskItems.Add(new TaskItem
            {
                Title = "Done Task 1",
                ProjectId = 1,
                AssignedToEmployeeId = 1,
                Status = TaskStatus.Completed,
                EstimatedHours = 10m,
                ActualHours = 10m,
                CreatedByUserId = "u-1",
                RowVersion = Array.Empty<byte>()
            });
            db.TaskItems.Add(new TaskItem
            {
                Title = "Done Task 2",
                ProjectId = 1,
                AssignedToEmployeeId = 1,
                Status = TaskStatus.Cancelled,
                EstimatedHours = 5m,
                ActualHours = 1m,
                CreatedByUserId = "u-1",
                RowVersion = Array.Empty<byte>()
            });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var results = await service.GetWorkloadAsync(projectId: 1);

            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetWorkloadAsync_NullEstimatedHours_TreatedAsZero()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            db.TaskItems.Add(new TaskItem
            {
                Title = "No Estimate Task",
                ProjectId = 1,
                AssignedToEmployeeId = 1,
                Status = TaskStatus.InProgress,
                EstimatedHours = null,
                ActualHours = 5m,
                CreatedByUserId = "u-1",
                RowVersion = Array.Empty<byte>()
            });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var results = await service.GetWorkloadAsync(projectId: 1);

            results.Should().HaveCount(1);
            results[0].RemainingHours.Should().Be(0); // (0 - 5) clamped to 0
        }

        [Fact]
        public async Task GetWorkloadAsync_NegativeRemainingHours_ClampedToZero()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            db.TaskItems.Add(new TaskItem
            {
                Title = "Over-worked Task",
                ProjectId = 1,
                AssignedToEmployeeId = 1,
                Status = TaskStatus.InProgress,
                EstimatedHours = 5m,
                ActualHours = 15m,
                CreatedByUserId = "u-1",
                RowVersion = Array.Empty<byte>()
            });
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var results = await service.GetWorkloadAsync(projectId: 1);

            results[0].RemainingHours.Should().BeGreaterThanOrEqualTo(0);
        }

        #endregion

        #region Caching

        [Fact]
        public async Task GetWorkloadAsync_CachesResult()
        {
            using var db = TestDbContextFactory.Create();
            SeedProjectWithEmployees(db);
            SeedTasks(db, employeeId: 1, count: 2, estimatedHours: 10m, actualHours: 2m);
            await db.SaveChangesAsync();

            var service = CreateService(db);

            var first = await service.GetWorkloadAsync(projectId: 1);
            // Add more tasks after first call
            SeedTasks(db, employeeId: 1, count: 3, estimatedHours: 10m, actualHours: 1m, startId: 100);
            await db.SaveChangesAsync();
            var second = await service.GetWorkloadAsync(projectId: 1);

            // Should return cached result
            first.Should().BeEquivalentTo(second);
        }

        #endregion

        #region Seed Helpers

        private static void SeedProjectWithEmployees(ApplicationDbContext db)
        {
            var dept = new Department
            {
                Id = 1,
                DepartmentCode = "ENG",
                DepartmentName = "Engineering"
            };
            db.Departments.Add(dept);

            var emp = new Employee
            {
                Id = 1,
                FirstName = "Test",
                LastName = "Employee",
                Email = "test@test.com",
                PhoneNumber = "123",
                Position = "Dev",
                HireDate = DateTime.UtcNow,
                Salary = 50000,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                ImageUrl = "img.png",
                Gender = Gender.Male,
                DepartmentId = 1
            };
            db.Employees.Add(emp);

            var project = new Project
            {
                Id = 1,
                ProjectName = "Test Project",
                ProjectCode = "TST-001",
                DepartmentId = 1,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(2),
                Budget = 100000m
            };
            db.Projects.Add(project);
            db.SaveChanges();
        }

        private static void SeedTasks(ApplicationDbContext db, int employeeId, int count,
            decimal estimatedHours, decimal actualHours, int startId = 1)
        {
            for (int i = 0; i < count; i++)
            {
                db.TaskItems.Add(new TaskItem
                {
                    Title = $"Task {startId + i}",
                    ProjectId = 1,
                    AssignedToEmployeeId = employeeId,
                    Status = TaskStatus.InProgress,
                    Priority = TaskPriority.Medium,
                    EstimatedHours = estimatedHours,
                    ActualHours = actualHours,
                    DueDate = DateTime.UtcNow.AddDays(7),
                    CreatedByUserId = "u-1",
                    RowVersion = Array.Empty<byte>()
                });
            }
        }

        #endregion
    }
}
