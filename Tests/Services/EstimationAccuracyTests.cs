using ERP.BLL.Reporting.Services;
using FluentAssertions;

namespace Tests.Services
{
    public class EstimationAccuracyTests
    {
        #region ClassifyAccuracy Tests

        [Theory]
        [InlineData(0.5, "Underestimated")]
        [InlineData(0.79, "Underestimated")]
        [InlineData(0.8, "Accurate")]
        [InlineData(1.0, "Accurate")]
        [InlineData(1.2, "Accurate")]
        [InlineData(1.21, "Overestimated")]
        [InlineData(2.0, "Overestimated")]
        public void ClassifyAccuracy_ReturnsExpectedLabel(double ratio, string expectedLabel)
        {
            var result = ReportingService.ClassifyAccuracy(ratio);
            result.Should().Be(expectedLabel);
        }

        [Fact]
        public void ClassifyAccuracy_ExactBoundary_LowerBound_ReturnsAccurate()
        {
            ReportingService.ClassifyAccuracy(0.8).Should().Be("Accurate");
        }

        [Fact]
        public void ClassifyAccuracy_ExactBoundary_UpperBound_ReturnsAccurate()
        {
            ReportingService.ClassifyAccuracy(1.2).Should().Be("Accurate");
        }

        [Fact]
        public void ClassifyAccuracy_JustBelowLower_ReturnsUnderestimated()
        {
            ReportingService.ClassifyAccuracy(0.799).Should().Be("Underestimated");
        }

        [Fact]
        public void ClassifyAccuracy_JustAboveUpper_ReturnsOverestimated()
        {
            ReportingService.ClassifyAccuracy(1.201).Should().Be("Overestimated");
        }

        [Fact]
        public void ClassifyAccuracy_ZeroRatio_ReturnsUnderestimated()
        {
            ReportingService.ClassifyAccuracy(0.0).Should().Be("Underestimated");
        }

        #endregion
    }
}
