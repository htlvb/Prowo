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
using System.Text.Json;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class CreateProjectTests
{
    [Fact]
    public async Task CantCreateProjectWhenNotAuthorized()
    {
        var host = await StartHost();
        var client = host.GetTestClient();
        var project = EditingProjectDataDtoFaker.Generate();
        var writerId = ProjectOrganizers.Select(v => v.Id).First(v => v != project.OrganizerId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"project-writer-{writerId}");

        var response = await client.PostAsJsonAsync("/api/projects", project, new JsonSerializerOptions().AddConverters());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CantCreateProjectWhenDateIsInThePast()
    {
        var host = await StartHost();
        var client = host.GetTestClient();
        var project = EditingProjectDataDtoFaker.Generate() with { Date = DateOnly.FromDateTime(DateTime.Today).AddDays(-1) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"project-writer-{project.OrganizerId}");

        var response = await client.PostAsJsonAsync("/api/projects", project, new JsonSerializerOptions().AddConverters());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Project too old.", await response.Content.ReadAsStringAsync());
    }

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
                            .AddAuthentication(TestAuthHandler.SchemeName)
                            .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, options => { });
                        services.AddProwoAuthorizationRules();
                        services.AddProwoControllers();
                        services.AddSingleton<IUserStore>(new InMemoryUserStore(ProjectOrganizers));
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

    private static Faker<EditingProjectDataDto> EditingProjectDataDtoFaker { get; } = new Faker<EditingProjectDataDto>()
        .CustomInstantiator(v =>
        {
            var date = v.Date.SoonDateOnly(20);
            var organizerIds = ProjectOrganizers
                .OrderBy(_ => v.Random.Double())
                .Take(v.Random.Number(1, 5))
                .Select(v => v.Id)
                .ToList();
            return new EditingProjectDataDto(
                v.Random.Words(),
                v.Lorem.Text(),
                v.Address.BuildingNumber(),
                organizerIds.First(),
                organizerIds.Skip(1).ToList(),
                date,
                v.Date.BetweenTimeOnly(new TimeOnly(7, 0), new TimeOnly(9, 0)),
                v.Random.Bool() ? v.Date.BetweenTimeOnly(new TimeOnly(12, 0), new TimeOnly(15, 0)) : null,
                new DateTime(v.Date.Between(v.Date.Recent(-5), date.ToDateTime(TimeOnly.MinValue)).Ticks, DateTimeKind.Unspecified),
                v.Random.Number(1, 500)
            );
        });

    private static IReadOnlyList<ProjectOrganizer> ProjectOrganizers { get; } =
        new Faker<ProjectOrganizer>()
            .CustomInstantiator(v => new ProjectOrganizer(
                v.Random.Uuid().ToString(),
                v.Name.FirstName(),
                v.Name.LastName(),
                v.Random.String2(4).ToUpper()
            ))
        .Generate(10);
}
