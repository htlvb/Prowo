using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prowo.WebAsm.Server.Data;

namespace Prowo.WebAsm.Server.IntegrationTests.Utils;

public static class InMemoryServer
{
    public static async Task<IHost> Start()
    {
        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services
                            .AddAuthentication(TestAuthHandler.SchemeName)
                            .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, options => { });
                        services.AddProwoAuthorizationRules();
                        services.AddProwoControllers();
                        services.AddSingleton<IUserStore>(new InMemoryUserStore(FakeData.ProjectOrganizers));
                        services.AddSingleton<IProjectStore, InMemoryProjectStore>();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();

                        app.UseAuthentication();
                        app.UseAuthorization();

                        app.UseEndpoints(config => config.MapControllers());
                    });
            })
            .StartAsync();
    }
}
