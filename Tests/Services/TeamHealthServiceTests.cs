using ERP.BLL.Services;
using FluentAssertions;

namespace Tests.Services
{
    public class TeamHealthServiceTests
    {
        #region ClassifyHealth
        [Theory]
        [InlineData(100, "Healthy")]
        [InlineData(80, "Healthy")]
        [InlineData(79, "Attention")]
        [InlineData(60, "Attention")]
        [InlineData(59, "At Risk")]
        [InlineData(0, "At Risk")]
        public void ClassifyHealth_ReturnsCorrectStatus(int score, string expected)
        {
            TeamHealthService.ClassifyHealth(score).Should().Be(expected);
        }

        [Fact]
        public void ClassifyHealth_BoundaryAt80_IsHealthy()
        {
            TeamHealthService.ClassifyHealth(80).Should().Be("Healthy");
        }

        [Fact]
        public void ClassifyHealth_BoundaryAt60_IsAttention()
        {
            TeamHealthService.ClassifyHealth(60).Should().Be("Attention");
        }

        [Fact]
        public void ClassifyHealth_NegativeScore_IsAtRisk()
        {
            // Because score is clamped before classification in real usage, but test raw method
            TeamHealthService.ClassifyHealth(-10).Should().Be("At Risk");
        }
        #endregion

        #region Deduction Constants
        [Fact]
        public void Deduction_Constants_AreCorrect()
        {
            TeamHealthService.OverdueDeduction.Should().Be(15);
            TeamHealthService.BlockedDeduction.Should().Be(10);
            TeamHealthService.OverloadedDeduction.Should().Be(10);
            TeamHealthService.BehindScheduleDeduction.Should().Be(15);
        }

        [Fact]
        public void Thresholds_AreCorrect()
        {
            TeamHealthService.OverdueThreshold.Should().Be(0.10);
            TeamHealthService.BlockedThreshold.Should().Be(0.05);
            TeamHealthService.OverloadedLoadScoreThreshold.Should().Be(70);
        }
        #endregion

        #region Score Scenarios
        [Fact]
        public void MaxDeduction_ScoreCannotExceed100()
        {
            // Starting at 100, maximum deductions: 15 + 10 + 10 + 15 = 50
            // So minimum health score = 50 when all risk factors are present
            var maxDeductions = TeamHealthService.OverdueDeduction +
                              TeamHealthService.BlockedDeduction +
                              TeamHealthService.OverloadedDeduction +
                              TeamHealthService.BehindScheduleDeduction;
            var lowestPossible = 100 - maxDeductions;
            lowestPossible.Should().Be(50);
        }
        #endregion
    }
}
