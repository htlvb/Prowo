using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
using System.Net;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class DeleteProjectTests
{
    [Fact]
    public async Task CantDeleteOtherProject()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var project = FakeData.ProjectFaker.Generate()
            with { AllAttendees = Array.Empty<ProjectAttendee>() };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter("1234"); // TODO use real id from IUserStore?

        using var response = await client.DeleteAsync($"/api/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CantDeleteProjectWithAttendees()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var project = FakeData.ProjectFaker.Generate()
            with { AllAttendees = FakeData.ProjectAttendees.Take(1).ToList() };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter(project.Organizer.Id);

        using var response = await client.DeleteAsync($"/api/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CantDeleteProjectFromThePast()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var project = FakeData.PastProjectFaker.Generate()
            with { AllAttendees = Array.Empty<ProjectAttendee>() };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter(project.Organizer.Id);

        using var response = await client.DeleteAsync($"/api/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CanDeleteOwnProjectWithoutAttendee()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var project = FakeData.ProjectFaker.Generate()
            with { AllAttendees = Array.Empty<ProjectAttendee>() };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter(project.Organizer.Id);

        using var response = await client.DeleteAsync($"/api/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CanDeleteOtherProjectWhenAuthorized()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var project = FakeData.ProjectFaker.Generate()
            with { AllAttendees = Array.Empty<ProjectAttendee>() };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient()
            .AuthenticateAsAllProjectWriter("1234"); // TODO use real id from IUserStore?

        using var response = await client.DeleteAsync($"/api/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
