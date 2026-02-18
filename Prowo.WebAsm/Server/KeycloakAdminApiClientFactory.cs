using Keycloak.AdminApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

public class KeycloakAdminApiClientFactory(string baseUrl, KeycloakAccessTokenProvider accessTokenProvider)
{
    public async Task<KeycloakAdminApiClient> CreateClient()
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(accessTokenProvider);
        var adapter = new HttpClientRequestAdapter(authProvider)
        {
            BaseUrl = baseUrl
        };
        return new KeycloakAdminApiClient(adapter);
    }
}

public class KeycloakAccessTokenProvider(string hostName, IHttpContextAccessor httpContextAccessor) : IAccessTokenProvider
{
    public AllowedHostsValidator AllowedHostsValidator => new([hostName]);

    public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        if (httpContextAccessor.HttpContext == null)
        {
            throw new Exception("HTTP context not available.");
        }
        var token = await httpContextAccessor.HttpContext.GetTokenAsync("access_token");
        if (token == null)
        {
            throw new Exception("Access token not found.");
        }
        return token;
    }
}