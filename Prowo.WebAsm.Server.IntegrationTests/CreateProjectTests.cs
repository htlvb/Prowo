using Bogus;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Shared;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class CreateProjectTests
{
    [Fact]
    public async Task CantCreateProjectWhenNotAuthorized()
    {
        var host = await StartHost();
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "test", "1");

        var response = await client.PostAsJsonAsync("/api/projects", EditingProjectDataDtoFaker.Generate());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Faker<EditingProjectDataDto> EditingProjectDataDtoFaker { get; } = new Faker<EditingProjectDataDto>()
        .CustomInstantiator(v =>
        {
            var date = v.Date.SoonDateOnly(20);
            return new EditingProjectDataDto(
                v.Random.Words(),
                v.Lorem.Text(),
                v.Address.BuildingNumber(),
                v.Random.Uuid().ToString(),
                Enumerable.Repeat(0, v.Random.Number(0, 5)).Select(_ => v.Random.Uuid().ToString()).ToList(),
                date,
                v.Date.BetweenTimeOnly(new TimeOnly(7, 0), new TimeOnly(9, 0)),
                v.Random.Bool() ? v.Date.BetweenTimeOnly(new TimeOnly(12, 0), new TimeOnly(15, 0)) : null,
                v.Date.Between(v.Date.Recent(-5), date.ToDateTime(TimeOnly.MinValue)),
                v.Random.Number(1, 500)
            );
        });

    private static async Task<IHost> StartHost()
    {
        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services
                            .AddAuthentication(defaultScheme: "test")
                            .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(authenticationScheme: "test", options => { });
                        services.AddProwoAuthorizationRules();
                        services.AddProwoControllers();
                        services.AddSingleton<IUserStore, InMemoryUserStore>();
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
