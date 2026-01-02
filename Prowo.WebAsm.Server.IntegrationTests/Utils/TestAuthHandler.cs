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
        UrlEncoder encoder) : base(options, logger, encoder)
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
            else if (authHeader.Parameter.StartsWith("project-writer-"))
            {
                claims.Add(new Claim(ClaimTypes.Role, "Project.Write"));
                claims.Add(new Claim("oid", authHeader.Parameter.Substring("project-writer-".Length)));
            }
            else if (authHeader.Parameter.StartsWith("all-project-writer-"))
            {
                claims.Add(new Claim(ClaimTypes.Role, "Project.Write.All"));
                claims.Add(new Claim("oid", authHeader.Parameter.Substring("all-project-writer-".Length)));
            }
            else if (authHeader.Parameter.StartsWith("report-creator-"))
            {
                claims.Add(new Claim(ClaimTypes.Role, "Report.Create"));
                claims.Add(new Claim("oid", authHeader.Parameter.Substring("report-creator-".Length)));
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

public static class HttpClientExtensions
{
    public static HttpClient AuthenticateAsProjectWriter(this HttpClient httpClient, string userId)
    {
        return httpClient.SetUser($"project-writer-{userId}");
    }

    public static HttpClient AuthenticateAsAllProjectWriter(this HttpClient httpClient, string userId)
    {
        return httpClient.SetUser($"all-project-writer-{userId}");
    }

    public static HttpClient AuthenticateAsProjectAttendee(this HttpClient httpClient, string userId)
    {
        return httpClient.SetUser($"project-attendee-{userId}");
    }

    public static HttpClient AuthenticateAsReportCreator(this HttpClient httpClient, string userId)
    {
        return httpClient.SetUser($"report-creator-{userId}");
    }

    private static HttpClient SetUser(this HttpClient httpClient, string userName)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName, userName);
        return httpClient;
    }
}
