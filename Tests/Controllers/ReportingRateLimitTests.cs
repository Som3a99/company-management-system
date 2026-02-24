using FluentAssertions;
using System.Net;
using Tests.Infrastructure;

namespace Tests.Controllers
{
    public class ReportingRateLimitTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public ReportingRateLimitTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ReportingEndpoints_ShouldEnforceRateLimitPolicy()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Auth", "rate-user");
            client.DefaultRequestHeaders.Add("X-Test-Roles", "CEO");

            HttpStatusCode? throttled = null;

            for (var i = 0; i < 40; i++)
            {
                var response = await client.GetAsync("/Reporting");
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throttled = response.StatusCode;
                    break;
                }
            }

            throttled.Should().Be(HttpStatusCode.TooManyRequests,
                "Reporting uses ReportingHeavy token bucket limiter and should throttle burst traffic");
        }
    }
}
