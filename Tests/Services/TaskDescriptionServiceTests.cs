using ERP.BLL.DTOs;
using ERP.BLL.Services;
using FluentAssertions;

namespace Tests.Services
{
    public class TaskDescriptionServiceTests
    {
        #region BuildPrompt Tests

        [Fact]
        public void BuildPrompt_WithProjectName_ContainsTitleAndProject()
        {
            var request = new GenerateTaskDescriptionRequest
            {
                Title = "Implement login page",
                ProjectName = "Web Portal"
            };

            var prompt = TaskDescriptionService.BuildPrompt(request);

            prompt.Should().Contain("Implement login page");
            prompt.Should().Contain("Web Portal");
            prompt.Should().Contain("Goal");
            prompt.Should().Contain("Steps");
            prompt.Should().Contain("Definition of Done");
        }

        [Fact]
        public void BuildPrompt_WithoutProjectName_OmitsProjectLine()
        {
            var request = new GenerateTaskDescriptionRequest
            {
                Title = "Fix bug #123",
                ProjectName = ""
            };

            var prompt = TaskDescriptionService.BuildPrompt(request);

            prompt.Should().Contain("Fix bug #123");
            prompt.Should().NotContain("belongs to the project");
        }

        #endregion

        #region BuildFallbackDescription Tests

        [Fact]
        public void FallbackDescription_WithProject_ContainsBothTitleAndProject()
        {
            var request = new GenerateTaskDescriptionRequest
            {
                Title = "Create dashboard widget",
                ProjectName = "Analytics Platform"
            };

            var description = TaskDescriptionService.BuildFallbackDescription(request);

            description.Should().Contain("Create dashboard widget");
            description.Should().Contain("Analytics Platform");
            description.Should().Contain("Goal:");
            description.Should().Contain("Steps:");
            description.Should().Contain("Definition of Done:");
        }

        [Fact]
        public void FallbackDescription_WithoutProject_DoesNotContainProjectContext()
        {
            var request = new GenerateTaskDescriptionRequest
            {
                Title = "Update README",
                ProjectName = ""
            };

            var description = TaskDescriptionService.BuildFallbackDescription(request);

            description.Should().Contain("Update README");
            description.Should().Contain("Goal:");
            description.Should().NotContain("for the  project");
        }

        [Fact]
        public void FallbackDescription_ContainsStructuredSections()
        {
            var request = new GenerateTaskDescriptionRequest
            {
                Title = "Test task",
                ProjectName = "Test Project"
            };

            var description = TaskDescriptionService.BuildFallbackDescription(request);

            description.Should().Contain("Goal:");
            description.Should().Contain("Steps:");
            description.Should().Contain("Definition of Done:");
            description.Should().Contain("Analyze requirements");
            description.Should().Contain("code review");
        }

        #endregion
    }
}
