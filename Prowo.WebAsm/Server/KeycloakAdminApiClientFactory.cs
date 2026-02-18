using Keycloak.AdminApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

public class KeycloakAdminApiClientFactory(string baseUrl, IHttpContextAccessor httpContextAccessor)
{
    public async Task<KeycloakAdminApiClient> CreateClient()
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
        var authProvider = new ApiKeyAuthenticationProvider($"Bearer {token}", "Authorization", ApiKeyAuthenticationProvider.KeyLocation.Header);
        var adapter = new HttpClientRequestAdapter(authProvider)
        {
            BaseUrl = baseUrl
        };
        return new KeycloakAdminApiClient(adapter);
    }
}