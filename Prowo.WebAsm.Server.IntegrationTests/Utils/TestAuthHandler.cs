using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Prowo.WebAsm.Server.IntegrationTests.Utils;

public class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<TestAuthHandlerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>();

        foreach (var auth in Context.Request.Headers.Authorization)
        {
            if (!AuthenticationHeaderValue.TryParse(auth, out var authHeader)) { continue; }
            if (authHeader.Scheme != SchemeName || authHeader.Parameter == null) continue;

            claims.Add(new Claim(ClaimTypes.Name, authHeader.Parameter));
            if (authHeader.Parameter.StartsWith("project-attendee-"))
            {
                claims.Add(new Claim(ClaimTypes.Role, "Project.Attend"));
                claims.Add(new Claim("oid", authHeader.Parameter.Substring("project-attendee-".Length)));
            }
            if (authHeader.Parameter.StartsWith("project-writer-"))
            {
                claims.Add(new Claim(ClaimTypes.Role, "Project.Write"));
                claims.Add(new Claim("oid", authHeader.Parameter.Substring("project-writer-".Length)));
            }
        }

        if (claims.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        else
        {
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

public class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
}
