using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
using Prowo.WebAsm.Shared;
using System.Net;
using System.Net.Http.Json;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class GetProjectListTests
{
    [Fact]
    public async Task CantGetProjectListWhenNotAuthenticated()
    {
        using var host = await InMemoryServer.Start();
        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CanGetProjectListWhenAuthenticated()
    {
        using var host = await InMemoryServer.Start();
        using var client = host.GetTestClient()
            .AuthenticateAsProjectAttendee("1234"); // TODO use real id from IUserStore?

        using var response = await client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProjectListDoesntContainOutdatedProjects()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var futureProjects = FakeData.ProjectFaker.Generate(100);
        var pastProjects = FakeData.PastProjectFaker.Generate(150);
        var allProjects = pastProjects.Concat(futureProjects).OrderBy(_ => Random.Shared.NextDouble());
        foreach (var project in allProjects)
        {
            await projectStore.CreateProject(project);
        }
        using var client = host.GetTestClient()
            .AuthenticateAsProjectAttendee("1234"); // TODO use real id from IUserStore?

        var actualProjects = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Equal(futureProjects.Count, actualProjects.Projects.Count);
    }

    [Fact]
    public async Task ShowAllAttendeesLinkIsNotEmptyIfAuthorizedAndAtLeastOneActiveProjectExists()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(FakeData.ProjectFaker.Generate());
        using var client = host.GetTestClient()
            .AuthenticateAsReportCreator("1234"); // TODO use real id from IUserStore?

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.NotNull(projectList.Links.ShowAllAttendees);
    }

    [Fact]
    public async Task ShowAllAttendeesLinkIsEmptyIfNotAuthorized()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(FakeData.ProjectFaker.Generate());
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter("1234"); // TODO use real id from IUserStore?

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Null(projectList.Links.ShowAllAttendees);
    }

    [Fact]
    public async Task ShowAllAttendeesLinkIsEmptyIfNoActiveProjectExists()
    {
        using var host = await InMemoryServer.Start();
        using var client = host.GetTestClient()
            .AuthenticateAsReportCreator("1234"); // TODO use real id from IUserStore?

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Null(projectList.Links.ShowAllAttendees);
    }
}
