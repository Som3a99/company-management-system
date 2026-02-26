using ERP.BLL.DTOs;
using ERP.BLL.Services;
using FluentAssertions;

namespace Tests.Services
{
    public class TaskAssignmentSuggestionServiceTests
    {
        #region ExtractKeywords
        [Fact]
        public void ExtractKeywords_NullInput_ReturnsEmpty()
        {
            var result = TaskAssignmentSuggestionService.ExtractKeywords(null);
            result.Should().BeEmpty();
        }

        [Fact]
        public void ExtractKeywords_EmptyString_ReturnsEmpty()
        {
            var result = TaskAssignmentSuggestionService.ExtractKeywords("   ");
            result.Should().BeEmpty();
        }

        [Fact]
        public void ExtractKeywords_FilteresStopWords()
        {
            var result = TaskAssignmentSuggestionService.ExtractKeywords("Fix the login page for users");
            result.Should().NotContain("the");
            result.Should().NotContain("for");
            result.Should().NotContain("fix");
        }

        [Fact]
        public void ExtractKeywords_ExtractsSignificantWords()
        {
            var result = TaskAssignmentSuggestionService.ExtractKeywords("Design user authentication module");
            result.Should().Contain("design");
            result.Should().Contain("user");
            result.Should().Contain("authentication");
            result.Should().Contain("module");
        }

        [Fact]
        public void ExtractKeywords_SplitsOnDelimiters()
        {
            var result = TaskAssignmentSuggestionService.ExtractKeywords("backend-api/endpoint.review");
            result.Should().Contain("backend");
            result.Should().Contain("api");
            result.Should().Contain("endpoint");
            result.Should().Contain("review");
        }

        [Fact]
        public void ExtractKeywords_ShortWordsFiltered()
        {
            var result = TaskAssignmentSuggestionService.ExtractKeywords("Do UI QA test cleanup");
            result.Should().NotContain("UI");
            result.Should().NotContain("QA");
        }

        [Fact]
        public void ExtractKeywords_DeduplicatesResults()
        {
            var result = TaskAssignmentSuggestionService.ExtractKeywords("design design DESIGN");
            result.Should().HaveCount(1);
            result.Should().Contain("design");
        }
        #endregion

        #region CalculateExperienceScore
        [Fact]
        public void ExperienceScore_NoData_ReturnsZero()
        {
            var map = new Dictionary<int, EmployeeExperienceData>();
            var score = TaskAssignmentSuggestionService.CalculateExperienceScore(1, new List<string> { "api" }, map);
            score.Should().Be(0);
        }

        [Fact]
        public void ExperienceScore_NoKeywords_ReturnsBaseExperience()
        {
            var map = new Dictionary<int, EmployeeExperienceData>
            {
                [1] = new EmployeeExperienceData
                {
                    EmployeeId = 1,
                    CompletedTitles = new List<string> { "Task A", "Task B", "Task C" },
                    CompletedCount = 3
                }
            };

            var score = TaskAssignmentSuggestionService.CalculateExperienceScore(1, new List<string>(), map);
            // 3 tasks * 3 = 9 (base, up to 30)
            score.Should().Be(9);
        }

        [Fact]
        public void ExperienceScore_AllKeywordsMatch_HighScore()
        {
            var map = new Dictionary<int, EmployeeExperienceData>
            {
                [1] = new EmployeeExperienceData
                {
                    EmployeeId = 1,
                    CompletedTitles = new List<string> { "Design API endpoint", "Review authentication flow" },
                    CompletedCount = 2
                }
            };

            var keywords = new List<string> { "design", "api", "endpoint" };
            var score = TaskAssignmentSuggestionService.CalculateExperienceScore(1, keywords, map);

            // base: 2*3=6, keyword: 3/3*70=70, total: 76 clamped to 76
            score.Should().Be(76);
        }

        [Fact]
        public void ExperienceScore_PartialKeywordMatch()
        {
            var map = new Dictionary<int, EmployeeExperienceData>
            {
                [1] = new EmployeeExperienceData
                {
                    EmployeeId = 1,
                    CompletedTitles = new List<string> { "Design API" },
                    CompletedCount = 1
                }
            };

            var keywords = new List<string> { "design", "authentication" };
            var score = TaskAssignmentSuggestionService.CalculateExperienceScore(1, keywords, map);

            // base: 1*3=3, keyword: 1/2*70=35, total: 38
            score.Should().Be(38);
        }

        [Fact]
        public void ExperienceScore_CappedAt100()
        {
            var map = new Dictionary<int, EmployeeExperienceData>
            {
                [1] = new EmployeeExperienceData
                {
                    EmployeeId = 1,
                    CompletedTitles = Enumerable.Range(0, 20).Select(i => "API design review task item").ToList(),
                    CompletedCount = 20
                }
            };

            var keywords = new List<string> { "api", "design", "review" };
            var score = TaskAssignmentSuggestionService.CalculateExperienceScore(1, keywords, map);

            // base: 20*3=60 capped to 30, keyword: 3/3*70=70, total: 100
            score.Should().Be(100);
        }
        #endregion

        #region CalculateAccuracyScore
        [Fact]
        public void AccuracyScore_NoData_ReturnsNeutral()
        {
            var map = new Dictionary<int, double>();
            var score = TaskAssignmentSuggestionService.CalculateAccuracyScore(1, map);
            score.Should().Be(50);
        }

        [Fact]
        public void AccuracyScore_PerfectRatio_Returns100()
        {
            var map = new Dictionary<int, double> { [1] = 1.0 };
            var score = TaskAssignmentSuggestionService.CalculateAccuracyScore(1, map);
            score.Should().Be(100);
        }

        [Fact]
        public void AccuracyScore_DoubleEstimate_Returns0()
        {
            var map = new Dictionary<int, double> { [1] = 2.0 };
            var score = TaskAssignmentSuggestionService.CalculateAccuracyScore(1, map);
            // deviation = 1.0, score = max(0, 100 - 100) = 0
            score.Should().Be(0);
        }

        [Fact]
        public void AccuracyScore_SlightlyOver_Returns70()
        {
            var map = new Dictionary<int, double> { [1] = 1.3 };
            var score = TaskAssignmentSuggestionService.CalculateAccuracyScore(1, map);
            // deviation = 0.3, score = max(0, 100 - 30) = 70
            score.Should().Be(70);
        }

        [Fact]
        public void AccuracyScore_UnderEstimate_ScalesDown()
        {
            var map = new Dictionary<int, double> { [1] = 0.5 };
            var score = TaskAssignmentSuggestionService.CalculateAccuracyScore(1, map);
            // deviation = 0.5, score = 50
            score.Should().Be(50);
        }
        #endregion

        #region CalculateAvailabilityScore
        [Fact]
        public void AvailabilityScore_NoWorkloadData_Returns80()
        {
            var map = new Dictionary<int, int>();
            var score = TaskAssignmentSuggestionService.CalculateAvailabilityScore(1, map);
            score.Should().Be(80);
        }

        [Fact]
        public void AvailabilityScore_ZeroLoad_Returns100()
        {
            var map = new Dictionary<int, int> { [1] = 0 };
            var score = TaskAssignmentSuggestionService.CalculateAvailabilityScore(1, map);
            score.Should().Be(100);
        }

        [Fact]
        public void AvailabilityScore_MaxLoad_Returns0()
        {
            var map = new Dictionary<int, int> { [1] = 100 };
            var score = TaskAssignmentSuggestionService.CalculateAvailabilityScore(1, map);
            score.Should().Be(0);
        }

        [Fact]
        public void AvailabilityScore_ModerateLoad_ReturnsInverse()
        {
            var map = new Dictionary<int, int> { [1] = 60 };
            var score = TaskAssignmentSuggestionService.CalculateAvailabilityScore(1, map);
            score.Should().Be(40);
        }
        #endregion
    }
}
