using FluentAssertions;
using System.Net;
using Tests.Infrastructure;
namespace Tests.Security
{
    public class SecurityIntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public SecurityIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task RequireDepartmentManager_ShouldReturnForbidden_WithoutManagedDepartmentClaim()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Auth", "u-dept");
            client.DefaultRequestHeaders.Add("X-Test-Roles", "DepartmentManager");

            var response = await client.GetAsync("/DepartmentManagerHr");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task RequireDepartmentManager_ShouldAllow_WhenManagedDepartmentClaimExists()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Auth", "u-dept");
            client.DefaultRequestHeaders.Add("X-Test-Roles", "DepartmentManager");
            client.DefaultRequestHeaders.Add("X-Test-Claims", "ManagedDepartmentId=1");

            var response = await client.GetAsync("/DepartmentManagerHr");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RequireCEO_ShouldForbid_NonCeoOnAuditEndpoint()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Auth", "u-pm");
            client.DefaultRequestHeaders.Add("X-Test-Roles", "ProjectManager");

            var response = await client.GetAsync("/Reporting/Audit");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task RequireCEO_ShouldAllow_CeoOnAuditEndpoint()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Auth", "u-ceo");
            client.DefaultRequestHeaders.Add("X-Test-Roles", "CEO");

            var response = await client.GetAsync("/Reporting/Audit");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
