using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Tests.Security
{
    public class AuthorizationPolicyTests
    {
        private readonly IAuthorizationService _authorizationService;

        public AuthorizationPolicyTests()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorizationBuilder()
                .AddPolicy("RequireCEO", policy => policy.RequireRole("CEO"))
                .AddPolicy("RequireManager", policy => policy.RequireRole("CEO", "DepartmentManager", "ProjectManager"))
                .AddPolicy("RequireDepartmentManager", policy => policy.RequireClaim("ManagedDepartmentId"))
                .AddPolicy("RequireProjectManager", policy => policy.RequireClaim("ManagedProjectId"));

            _authorizationService = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        }

        [Fact]
        public async Task RequireCEO_ShouldDenyNonCeoUser()
        {
            var principal = Principal(roles: new[] { "Employee" });
            var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireCEO");
            result.Succeeded.Should().BeFalse();
        }

        [Fact]
        public async Task RequireManager_ShouldAllowProjectManager()
        {
            var principal = Principal(roles: new[] { "ProjectManager" });
            var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireManager");
            result.Succeeded.Should().BeTrue();
        }

        [Fact]
        public async Task RequireDepartmentManager_ShouldRequireManagedDepartmentClaim()
        {
            var principal = Principal(claims: new[] { new Claim("ManagedDepartmentId", "5") });
            var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireDepartmentManager");
            result.Succeeded.Should().BeTrue();
        }

        [Fact]
        public async Task RequireProjectManager_ShouldRequireManagedProjectClaim()
        {
            var principal = Principal(claims: new[] { new Claim("ManagedProjectId", "7") });
            var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireProjectManager");
            result.Succeeded.Should().BeTrue();
        }

        private static ClaimsPrincipal Principal(IEnumerable<string>? roles = null, IEnumerable<Claim>? claims = null)
        {
            var allClaims = new List<Claim>(claims ?? Enumerable.Empty<Claim>())
        {
            new(ClaimTypes.NameIdentifier, "u1")
        };

            foreach (var role in roles ?? Enumerable.Empty<string>())
            {
                allClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(allClaims, "TestAuth"));

        }
    }
}
