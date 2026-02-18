using Duende.IdentityModel.OidcClient;
using Microsoft.Kiota.Abstractions.Authentication;

class KeycloakAccessTokenProvider(string host) : IAccessTokenProvider
{
    private readonly AutoResetEvent waiter = new(initialState: true);
    private string? accessToken;

    public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator([host]);

    public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        waiter.WaitOne();
        try
        {
            // TODO check lifetime
            if (accessToken != null) return accessToken;

            await using var listener = await LoopbackHttpListener.Start(7206);
            var browser = new SystemBrowser(listener);

            var options = new OidcClientOptions
            {
                Authority = "https://id.htlvb.at/realms/htlvb",
                ClientId = "prowo",
                RedirectUri = $"{listener.ServerUrl}/authentication/login-callback",
                Scope = "openid",
                Browser = browser,
                DisablePushedAuthorization = true
            };

            var oidcClient = new OidcClient(options);
            var result = await oidcClient.LoginAsync(new LoginRequest());
            if (result.IsError)
            {
                throw new Exception($"Error while getting access token: {result.ErrorDescription} ({result.Error})");
            }
            return accessToken = result.AccessToken;
        }
        finally
        {
            waiter.Set();
        }
    }
}