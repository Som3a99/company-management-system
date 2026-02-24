using FluentAssertions;
using System.Net;
using Tests.Infrastructure;

namespace Tests.Controllers
{
    public class EndpointIntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public EndpointIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/Department")]
        [InlineData("/Project")]
        [InlineData("/Reporting")]
        public async Task ProtectedEndpoints_ShouldRedirectUnauthenticatedUsers(string url)
        {
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.OriginalString.Should().Contain("/Account/Login");
        }

        [Theory]
        [InlineData("/Department", "CEO")]
        [InlineData("/Project", "CEO")]
        [InlineData("/Reporting", "CEO")]
        public async Task ManagerEndpoints_ShouldReturnSuccess_ForAuthorizedRole(string url, string role)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Auth", "integration-user");
            client.DefaultRequestHeaders.Add("X-Test-Roles", role);

            var response = await client.GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
