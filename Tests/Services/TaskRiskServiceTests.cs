using ERP.BLL.DTOs;
using ERP.BLL.Services;
using ERP.DAL.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace Tests.Services
{
    public class TaskRiskServiceTests
    {
        private readonly TaskRiskService _service;

        public TaskRiskServiceTests()
        {
            var logger = new Mock<ILogger<TaskRiskService>>();
            _service = new TaskRiskService(logger.Object);
        }

        #region Completed / Cancelled Tasks

        [Fact]
        public void CalculateRisk_CompletedTask_ReturnsLowZero()
        {
            var task = CreateTask(status: TaskStatus.Completed);
            var result = _service.CalculateRisk(task);

            result.Score.Should().Be(0);
            result.Level.Should().Be("Low");
        }

        [Fact]
        public void CalculateRisk_CancelledTask_ReturnsLowZero()
        {
            var task = CreateTask(status: TaskStatus.Cancelled);
            var result = _service.CalculateRisk(task);

            result.Score.Should().Be(0);
            result.Level.Should().Be("Low");
        }

        #endregion

        #region Overdue Tasks

        [Fact]
        public void CalculateRisk_OverdueTask_ScoreIncludes40()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(-3),
                status: TaskStatus.InProgress);

            var result = _service.CalculateRisk(task);

            // Overdue (+40) + InProgress (+10) + low progress near deadline (+20) = 70+
            result.Score.Should().BeGreaterThanOrEqualTo(50);
            result.Level.Should().BeOneOf("Medium", "High");
            result.Reason.Should().Contain("overdue");
        }

        [Fact]
        public void CalculateRisk_OverdueBlockedCritical_ReturnsHigh()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(-1),
                status: TaskStatus.Blocked,
                priority: TaskPriority.Critical);

            var result = _service.CalculateRisk(task);

            // Overdue (+40) + Blocked (+25) + Critical (+20) = 85
            result.Score.Should().BeGreaterThanOrEqualTo(70);
            result.Level.Should().Be("High");
            result.Reason.Should().Contain("overdue");
            result.Reason.Should().Contain("blocked");
        }

        #endregion

        #region Near Due Date

        [Fact]
        public void CalculateRisk_DueIn2Days_ScoreIncludes25()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(2),
                status: TaskStatus.New);

            var result = _service.CalculateRisk(task);

            result.Score.Should().BeGreaterThanOrEqualTo(25);
            result.Reason.Should().Contain("Due in");
        }

        [Fact]
        public void CalculateRisk_DueIn5Days_ScoreIncludes15()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(4),
                status: TaskStatus.New);

            var result = _service.CalculateRisk(task);

            result.Score.Should().BeGreaterThanOrEqualTo(15);
        }

        #endregion

        #region Blocked Tasks

        [Fact]
        public void CalculateRisk_BlockedTask_ScoreIncludes25()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(30),
                status: TaskStatus.Blocked);

            var result = _service.CalculateRisk(task);

            result.Score.Should().BeGreaterThanOrEqualTo(25);
            result.Reason.Should().Contain("blocked");
        }

        #endregion

        #region Progress & Hours

        [Fact]
        public void CalculateRisk_LowProgressNearDeadline_AddsBonusScore()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(2),
                status: TaskStatus.InProgress,
                estimatedHours: 40m,
                actualHours: 10m); // 25% progress

            var result = _service.CalculateRisk(task);

            // Due <=2 (+25) + InProgress (+10) + low progress near deadline (+20) = 55+
            result.Score.Should().BeGreaterThanOrEqualTo(55);
            result.Reason.Should().Contain("progress");
        }

        [Fact]
        public void CalculateRisk_HoursExceeded_ScoreIncludes15()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(10),
                status: TaskStatus.InProgress,
                estimatedHours: 10m,
                actualHours: 15m);

            var result = _service.CalculateRisk(task);

            result.Score.Should().BeGreaterThanOrEqualTo(25); // InProgress (+10) + hours exceeded (+15)
            result.Reason.Should().Contain("exceeded");
        }

        #endregion

        #region Zero / Null EstimatedHours

        [Fact]
        public void CalculateRisk_NullEstimatedHours_DoesNotThrow()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(10),
                status: TaskStatus.New,
                estimatedHours: null,
                actualHours: 5m);

            var result = _service.CalculateRisk(task);

            result.Should().NotBeNull();
            result.Score.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void CalculateRisk_ZeroEstimatedHours_DoesNotThrow()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(10),
                status: TaskStatus.InProgress,
                estimatedHours: 0m,
                actualHours: 3m);

            var result = _service.CalculateRisk(task);

            result.Should().NotBeNull();
        }

        #endregion

        #region No DueDate

        [Fact]
        public void CalculateRisk_NoDueDate_DoesNotCrash()
        {
            var task = CreateTask(
                dueDate: null,
                status: TaskStatus.InProgress);

            var result = _service.CalculateRisk(task);

            result.Should().NotBeNull();
            // Only InProgress (+10) penalty
            result.Score.Should().Be(10);
            result.Level.Should().Be("Low");
        }

        #endregion

        #region Score Clamping

        [Fact]
        public void CalculateRisk_ExtremeScenario_ClampedAt100()
        {
            // Overdue + blocked + critical + low progress + hours exceeded
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(-5),
                status: TaskStatus.Blocked,
                priority: TaskPriority.Critical,
                estimatedHours: 10m,
                actualHours: 15m);

            var result = _service.CalculateRisk(task);

            result.Score.Should().BeLessThanOrEqualTo(100);
            result.Level.Should().Be("High");
        }

        #endregion

        #region Risk Level Thresholds

        [Fact]
        public void CalculateRisk_Score39_IsLow()
        {
            // New task, due in 4 days, normal priority => 15 points
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(4),
                status: TaskStatus.New,
                priority: TaskPriority.Medium);

            var result = _service.CalculateRisk(task);

            result.Level.Should().Be("Low");
            result.Score.Should().BeLessThan(40);
        }

        [Fact]
        public void CalculateRisk_SafeTask_ReturnsLow()
        {
            var task = CreateTask(
                dueDate: DateTime.UtcNow.AddDays(30),
                status: TaskStatus.New,
                priority: TaskPriority.Low,
                estimatedHours: 10m,
                actualHours: 5m);

            var result = _service.CalculateRisk(task);

            result.Score.Should().BeLessThan(40);
            result.Level.Should().Be("Low");
        }

        #endregion

        #region Helper

        private static TaskItem CreateTask(
            TaskStatus status = TaskStatus.New,
            TaskPriority priority = TaskPriority.Medium,
            DateTime? dueDate = null,
            decimal? estimatedHours = 20m,
            decimal actualHours = 5m)
        {
            return new TaskItem
            {
                Id = 1,
                Title = "Test Task",
                Status = status,
                Priority = priority,
                DueDate = dueDate,
                EstimatedHours = estimatedHours,
                ActualHours = actualHours,
                CreatedByUserId = "user-1",
                RowVersion = Array.Empty<byte>()
            };
        }

        #endregion
    }
}
