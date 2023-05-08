using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
using System.Net;
using static Prowo.WebAsm.Server.IntegrationTests.CustomGenerators;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class DeregisterUserTests
{
    [Property(Arbitrary = new[] { typeof(CustomGenerators) })]
    public async Task CantDeregisterUserFromOtherProject(FutureProjectWithAttendees p, NonNegativeInt removeAt)
    {
        var project = p.Project;
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(project);
        var attendee = project.AllAttendees[removeAt.Get % project.AllAttendees.Count];
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter("1234"); // TODO use real id from IUserStore?

        using var response = await client.DeleteAsync($"/api/projects/{project.Id}/attendees/{attendee.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Property(Arbitrary = new[] { typeof(CustomGenerators) })]
    public async Task CanDeregisterUserFromOwnProject(FutureProjectWithAttendees p, NonNegativeInt removeAt)
    {
        var project = p.Project;
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(project);
        var attendee = project.AllAttendees[removeAt.Get % project.AllAttendees.Count];
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter(project.Organizer.Id);

        using var response = await client.DeleteAsync($"/api/projects/{project.Id}/attendees/{attendee.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    //[Fact]
    //public async Task CantDeleteProjectFromThePast()
    //{
    //    using var host = await InMemoryServer.Start();
    //    var projectStore = host.Services.GetRequiredService<IProjectStore>();
    //    var project = FakeData.PastProjectFaker.Generate()
    //        with { AllAttendees = Array.Empty<ProjectAttendee>() };
    //    await projectStore.CreateProject(project);
    //    using var client = host.GetTestClient()
    //        .AuthenticateAsProjectWriter(project.Organizer.Id);

    //    using var response = await client.DeleteAsync($"/api/projects/{project.Id}");

    //    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    //}

    //[Fact]
    //public async Task CanDeleteOwnProjectWithoutAttendee()
    //{
    //    using var host = await InMemoryServer.Start();
    //    var projectStore = host.Services.GetRequiredService<IProjectStore>();
    //    var project = FakeData.ProjectFaker.Generate()
    //        with { AllAttendees = Array.Empty<ProjectAttendee>() };
    //    await projectStore.CreateProject(project);
    //    using var client = host.GetTestClient()
    //        .AuthenticateAsProjectWriter(project.Organizer.Id);

    //    using var response = await client.DeleteAsync($"/api/projects/{project.Id}");

    //    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    //}

    //[Fact]
    //public async Task CanDeleteOtherProjectWhenAuthorized()
    //{
    //    using var host = await InMemoryServer.Start();
    //    var projectStore = host.Services.GetRequiredService<IProjectStore>();
    //    var project = FakeData.ProjectFaker.Generate()
    //        with { AllAttendees = Array.Empty<ProjectAttendee>() };
    //    await projectStore.CreateProject(project);
    //    using var client = host.GetTestClient()
    //        .AuthenticateAsAllProjectWriter("1234"); // TODO use real id from IUserStore?

    //    using var response = await client.DeleteAsync($"/api/projects/{project.Id}");

    //    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    //}
}
