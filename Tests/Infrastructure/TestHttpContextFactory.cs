using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Security.Claims;

namespace Tests.Infrastructure
{
    internal static class TestHttpContextFactory
    {
        public static IHttpContextAccessor CreateAccessor(
            IEnumerable<Claim>? claims = null,
            IEnumerable<string>? roles = null,
            bool withResponse = true)
        {
            var claimList = new List<Claim>(claims ?? Enumerable.Empty<Claim>());

            foreach (var role in roles ?? Enumerable.Empty<string>())
            {
                claimList.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claimList, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var context = new DefaultHttpContext
            {
                User = principal
            };

            if (!withResponse)
            {
                context.Features.Set<IHttpResponseFeature>(null!);
            }

            return new HttpContextAccessor { HttpContext = context };
        }
    }
}
