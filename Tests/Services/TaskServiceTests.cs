using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.BLL.Services;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Infrastructure;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace Tests.Services
{
    public class TaskServiceTests
    {
        [Fact]
        public void IsValidTransition_ShouldAllowKnownTransitions_AndDenyInvalidOnes()
        {
            using var db = TestDbContextFactory.Create();
            var service = CreateService(db);

            service.IsValidTransition(TaskStatus.New, TaskStatus.InProgress).Should().BeTrue();
            service.IsValidTransition(TaskStatus.Blocked, TaskStatus.Cancelled).Should().BeTrue();
            service.IsValidTransition(TaskStatus.InProgress, TaskStatus.Completed).Should().BeTrue();
            service.IsValidTransition(TaskStatus.Completed, TaskStatus.InProgress).Should().BeFalse();
        }

        [Fact]
        public async Task CreateTaskAsync_ShouldReturnInvalid_WhenTitleIsMissing()
        {
            using var db = TestDbContextFactory.Create();
            var service = CreateService(db);

            var request = new CreateTaskRequest(" ", "desc", 1, 1, TaskPriority.Medium, null, null, 2m);
            var result = await service.CreateTaskAsync(request, "u-1");

            result.Succeeded.Should().BeFalse();
            result.Error.Should().Be("Task title is required.");
        }

        [Fact]
        public async Task CreateTaskAsync_ShouldReturnForbidden_WhenUserCannotManageProject()
        {
            using var db = TestDbContextFactory.Create();

            var projectRepo = new Mock<IProjectRepository>();
            projectRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new Project { Id = 1, ProjectManagerId = 999, DepartmentId = 10, ProjectCode = "PRJ_001", ProjectName = "P1" });

            var employeeRepo = new Mock<IEmployeeRepository>();
            employeeRepo.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(new Employee { Id = 5, FirstName = "A", LastName = "B", Email = "a@x.com", PhoneNumber = "1234567", Position = "Dev", ImageUrl = "x", Gender = Gender.Male });

            var taskRepo = new Mock<ITaskRepository>();
            taskRepo.Setup(x => x.IsEmployeeAssignedToProjectAsync(5, 1)).ReturnsAsync(true);

            var accessor = TestHttpContextFactory.CreateAccessor(
                claims: new[] { new System.Security.Claims.Claim("EmployeeId", "10") },
                roles: new[] { "ProjectManager" });

            var service = new TaskService(
                taskRepo.Object,
                projectRepo.Object,
                employeeRepo.Object,
                Mock.Of<IUnitOfWork>(),
                accessor,
                db,
                Mock.Of<ICacheService>(),
                Mock.Of<ILogger<TaskService>>(),
                Mock.Of<INotificationService>(),
                Mock.Of<ITaskRiskService>());

            var req = new CreateTaskRequest("T", "D", 1, 5, TaskPriority.Low, null, null, 1m);
            var result = await service.CreateTaskAsync(req, "user-1");

            result.Succeeded.Should().BeFalse();
            result.Error.Should().Be("Forbidden.");
        }

        private static TaskService CreateService(ApplicationDbContext db)
        {
            return new TaskService(
                Mock.Of<ITaskRepository>(),
                Mock.Of<IProjectRepository>(),
                Mock.Of<IEmployeeRepository>(),
                Mock.Of<IUnitOfWork>(),
                TestHttpContextFactory.CreateAccessor(),
                db,
                Mock.Of<ICacheService>(),
                Mock.Of<ILogger<TaskService>>(),
                Mock.Of<INotificationService>(),
                Mock.Of<ITaskRiskService>());
        }
    }
}
