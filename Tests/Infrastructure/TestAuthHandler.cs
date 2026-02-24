using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Tests.Infrastructure
{
    internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string AuthScheme = "TestScheme";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Auth", out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"user-{userId}")
        };

            if (Request.Headers.TryGetValue("X-Test-Roles", out var roles))
            {
                foreach (var role in roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            if (Request.Headers.TryGetValue("X-Test-Claims", out var extraClaims))
            {
                foreach (var pair in extraClaims.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var kv = pair.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (kv.Length == 2)
                    {
                        claims.Add(new Claim(kv[0], kv[1]));
                    }
                }
            }

            var identity = new ClaimsIdentity(claims, AuthScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
