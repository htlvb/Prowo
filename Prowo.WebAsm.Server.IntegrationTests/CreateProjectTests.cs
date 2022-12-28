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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class CreateProjectTests
{
    [Fact]
    public async Task CanCreateProjectAsOrganizerWhenAuthorized()
    {
        var host = await StartHost();
        var client = host.GetTestClient();
        var project = EditingProjectDataDtoFaker.Generate();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"project-writer-{project.OrganizerId}");
        var projectStore = host.Services.GetService<IProjectStore>();
        var existingProjects = await projectStore.GetAllSince(DateTime.MinValue).ToList();

        var response = await client.PostAsJsonAsync("/api/projects", project, new JsonSerializerOptions().AddConverters());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actualNewProjects = (await projectStore.GetAllSince(DateTime.MinValue).ToList()).Except(existingProjects).ToList();
        Assert.Single(actualNewProjects);
        var isProjectValid = Project.TryCreateFromEditingProjectDataDto(project, actualNewProjects[0].Id, ProjectOrganizers.ToDictionary(v => v.Id), out var expectedNewProject, out _);
        Assert.True(isProjectValid, "Expected project to be valid");
        Assert.Equal(expectedNewProject, actualNewProjects[0], new ProjectEqualityComparer());
    }

    [Fact]
    public async Task CantCreateProjectWithOtherOrganizerWhenNotAuthorized()
    {
        var host = await StartHost();
        var client = host.GetTestClient();
        var project = EditingProjectDataDtoFaker.Generate();
        var writerId = ProjectOrganizers.First(v => v.Id != project.OrganizerId).Id;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"project-writer-{writerId}");

        var response = await client.PostAsJsonAsync("/api/projects", project, new JsonSerializerOptions().AddConverters());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public static IEnumerable<object[]> InvalidProjectData
    {
        get
        {
            yield return new object[] { "Title is empty", EditingProjectDataDtoFaker.Generate() with { Title = "" } };
            yield return new object[] { "Title is white-space", EditingProjectDataDtoFaker.Generate() with { Title = " " } };
            yield return new object[] { "Unknown organizer", EditingProjectDataDtoFaker.Generate() with { OrganizerId = "unknown-organizer" } };
            yield return new object[] { "Unknown co-organizer", EditingProjectDataDtoFaker.Generate() with { CoOrganizerIds = new[] { "unknown-co-organizer" } } };
            yield return new object[] { "Date is in the past", EditingProjectDataDtoFaker.Generate() with { Date = DateOnly.FromDateTime(DateTime.Today).AddDays(-1) } };
            yield return new object[] { "Start time and end time are invalid", EditingProjectDataDtoFaker.Generate() with { StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(9, 59) } };
            yield return new object[] { "Closing date is in the past", EditingProjectDataDtoFaker.Generate() with { ClosingDate = new DateTime(DateTime.Now.AddMinutes(-1).Ticks, DateTimeKind.Unspecified) } };
            yield return new object[] { "Max attendees is zero", EditingProjectDataDtoFaker.Generate() with { MaxAttendees = 0 } };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidProjectData))]
    public async Task CantCreateInvalidProject(string description, EditingProjectDataDto project)
    {
        var host = await StartHost();
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"project-writer-{project.OrganizerId}");

        var response = await client.PostAsJsonAsync("/api/projects", project, new JsonSerializerOptions().AddConverters());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
                new TimeOnly(7, 0).AddMinutes(v.Random.Number(0, 8) * 15),
                v.Random.Bool() ? new TimeOnly(12, 0).AddMinutes(v.Random.Number(0, 12) * 15) : null,
                new DateTime(v.Date.Between(v.Date.Soon(5), date.ToDateTime(TimeOnly.MinValue)).Ticks, DateTimeKind.Unspecified),
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

public class ProjectEqualityComparer : IEqualityComparer<Project>
{
    public bool Equals(Project? x, Project? y)
    {
        return Equals(x.Id, y.Id) &&
            Equals(x.Title, y.Title) &&
            Equals(x.Description, y.Description) &&
            Equals(x.Location, y.Location) &&
            Equals(x.Organizer, y.Organizer) &&
            x.CoOrganizers.SequenceEqual(y.CoOrganizers) &&
            Equals(x.Date, y.Date) &&
            Equals(x.StartTime, y.StartTime) &&
            Equals(x.EndTime, y.EndTime) &&
            Equals(x.ClosingDate, y.ClosingDate) &&
            Equals(x.MaxAttendees, y.MaxAttendees) &&
            x.AllAttendees.SequenceEqual(y.AllAttendees);
    }

    public int GetHashCode([DisallowNull] Project obj)
    {
        throw new NotImplementedException();
    }
}
