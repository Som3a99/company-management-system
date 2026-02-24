using ERP.PL.Helpers;
using FluentAssertions;

namespace Tests.Services.Helpers
{
    public class InputSanitizerTests
    {
        [Theory]
        [InlineData("<script>alert(1)</script>", "")]
        [InlineData("Hello onclick=\"x\"", "Hello \"x\"")]
        public void RemoveDangerousCharacters_ShouldStripScriptsAndHandlers(string input, string expected)
        {
            InputSanitizer.RemoveDangerousCharacters(input).Should().Be(expected);
        }

        [Fact]
        public void SanitizeEmail_ShouldNormalizeAndValidate()
        {
            InputSanitizer.SanitizeEmail(" Test.User@Example.com ").Should().Be("test.user@example.com");
            InputSanitizer.SanitizeEmail("not-an-email").Should().BeNull();
        }

        [Fact]
        public void SanitizeDepartmentCode_ShouldEnforcePattern()
        {
            InputSanitizer.SanitizeDepartmentCode("abc_123").Should().Be("ABC_123");
            InputSanitizer.SanitizeDepartmentCode("eng-1").Should().BeNull();
        }
    }
}
