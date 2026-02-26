using ERP.BLL.DTOs;
using ERP.BLL.Services;
using FluentAssertions;

namespace Tests.Services
{
    public class AiNarrativeServiceTests
    {
        #region BuildPrompt Tests

        [Fact]
        public void BuildPrompt_ContainsAllInputData()
        {
            var input = new ReportSummaryInput
            {
                TotalTasks = 100,
                CompletedTasks = 60,
                OverdueTasks = 10,
                BlockedTasks = 5,
                ActiveProjects = 8,
                DepartmentWithMostTasks = "Engineering"
            };

            var prompt = AiNarrativeService.BuildPrompt(input);

            prompt.Should().Contain("100");
            prompt.Should().Contain("60");
            prompt.Should().Contain("10");
            prompt.Should().Contain("5");
            prompt.Should().Contain("8");
            prompt.Should().Contain("Engineering");
            prompt.Should().Contain("executive");
        }

        [Fact]
        public void BuildPrompt_EmptyDepartment_StillGeneratesPrompt()
        {
            var input = new ReportSummaryInput
            {
                TotalTasks = 50,
                CompletedTasks = 25,
                OverdueTasks = 3,
                BlockedTasks = 0,
                ActiveProjects = 4,
                DepartmentWithMostTasks = ""
            };

            var prompt = AiNarrativeService.BuildPrompt(input);

            prompt.Should().NotBeNullOrWhiteSpace();
            prompt.Should().Contain("50");
        }

        #endregion

        #region BuildFallbackSummary Tests

        [Fact]
        public void FallbackSummary_ContainsCompletionRate()
        {
            var input = new ReportSummaryInput
            {
                TotalTasks = 100,
                CompletedTasks = 60,
                OverdueTasks = 10,
                BlockedTasks = 5,
                ActiveProjects = 8,
                DepartmentWithMostTasks = "Engineering"
            };

            var summary = AiNarrativeService.BuildFallbackSummary(input);

            summary.Should().Contain("60%");
            summary.Should().Contain("100 tasks");
            summary.Should().Contain("8 active projects");
            summary.Should().Contain("10 overdue");
            summary.Should().Contain("5 tasks are blocked");
            summary.Should().Contain("Engineering");
        }

        [Fact]
        public void FallbackSummary_ZeroOverdue_ShowsHealthy()
        {
            var input = new ReportSummaryInput
            {
                TotalTasks = 50,
                CompletedTasks = 30,
                OverdueTasks = 0,
                BlockedTasks = 0,
                ActiveProjects = 3,
                DepartmentWithMostTasks = "HR"
            };

            var summary = AiNarrativeService.BuildFallbackSummary(input);

            summary.Should().Contain("No tasks are currently overdue");
            summary.Should().Contain("No tasks are currently blocked");
        }

        [Fact]
        public void FallbackSummary_ZeroTotalTasks_ZeroCompletionRate()
        {
            var input = new ReportSummaryInput
            {
                TotalTasks = 0,
                CompletedTasks = 0,
                OverdueTasks = 0,
                BlockedTasks = 0,
                ActiveProjects = 0,
                DepartmentWithMostTasks = ""
            };

            var summary = AiNarrativeService.BuildFallbackSummary(input);

            summary.Should().Contain("0%");
            summary.Should().Contain("0 tasks");
        }

        [Fact]
        public void FallbackSummary_EmptyDepartment_ShowsGenericAdvice()
        {
            var input = new ReportSummaryInput
            {
                TotalTasks = 20,
                CompletedTasks = 10,
                OverdueTasks = 2,
                BlockedTasks = 0,
                ActiveProjects = 2,
                DepartmentWithMostTasks = ""
            };

            var summary = AiNarrativeService.BuildFallbackSummary(input);

            summary.Should().Contain("Task distribution across departments");
        }

        #endregion
    }
}
