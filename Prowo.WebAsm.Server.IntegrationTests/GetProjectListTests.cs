using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
using Prowo.WebAsm.Shared;
using System.Net.Http.Json;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class GetProjectListTests
{
    private static IEnumerable<ProjectDto> AllProjects(ProjectListDto list) =>
        list.Events.SelectMany(e => e.Projects);

    [Fact]
    public async Task CanGetProjectListWhenAuthenticated()
    {
        using var host = await InMemoryServer.Start();
        using var client = host.GetTestClient()
            .AuthenticateAsProjectAttendee("1234"); // TODO use real id from IUserStore?

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.NotNull(projectList);
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

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Equal(futureProjects.Count, AllProjects(projectList!).Count());
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

        Assert.NotNull(projectList!.Links.ShowAllAttendees);
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

        Assert.Null(projectList!.Links.ShowAllAttendees);
    }

    [Fact]
    public async Task ShowAllAttendeesLinkIsEmptyIfNoActiveProjectExists()
    {
        using var host = await InMemoryServer.Start();
        using var client = host.GetTestClient()
            .AuthenticateAsReportCreator("1234"); // TODO use real id from IUserStore?

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Null(projectList!.Links.ShowAllAttendees);
    }

    [Fact]
    public async Task CreateNewProjectLinkIsNotEmptyIfAuthorized()
    {
        using var host = await InMemoryServer.Start();
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter("1234"); // TODO use real id from IUserStore?

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.NotNull(projectList!.Links.CreateProject);
    }

    [Fact]
    public async Task CreateNewProjectLinkIsEmptyIfNotAuthorized()
    {
        using var host = await InMemoryServer.Start();
        using var client = host.GetTestClient()
            .AuthenticateAsReportCreator("1234"); // TODO use real id from IUserStore?

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Null(projectList!.Links.CreateProject);
    }

    [Fact]
    public async Task DeleteProjectLinkIsEmptyIfNotAuthorized()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var project = FakeData.ProjectFaker.Generate()
            with { AllAttendees = Array.Empty<ProjectAttendee>() };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter("1234"); // TODO use real id from IUserStore?

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Null(AllProjects(projectList!).Single().Links.Delete);
    }

    [Fact]
    public async Task DeleteProjectLinkIsNotEmptyIfAuthorized()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var project = FakeData.ProjectFaker.Generate()
            with { AllAttendees = Array.Empty<ProjectAttendee>() };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient()
            .AuthenticateAsProjectWriter(project.Organizer.Id);

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.NotNull(AllProjects(projectList!).Single().Links.Delete);
    }

    [Fact]
    public async Task ProjectsWithFutureVisibleFromAreHiddenFromAttendees()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var futureVisibleEvent = new Event(
            "future-visible-event",
            "Not Yet Visible",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(44)),
            DateTime.UtcNow.AddDays(10),
            DateTime.UtcNow.AddDays(15)
        );
        await eventStore.Create(futureVisibleEvent);
        var project = FakeData.ProjectFaker.Generate() with { EventId = futureVisibleEvent.Id };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient().AuthenticateAsProjectAttendee("1234");

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.DoesNotContain(AllProjects(projectList!), p => p.Title == project.Title);
    }

    [Fact]
    public async Task ProjectsWithFutureVisibleFromAreVisibleToAdmins()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var futureVisibleEvent = new Event(
            "future-visible-event-admin",
            "Not Yet Visible But Admin Sees",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(44)),
            DateTime.UtcNow.AddDays(10),
            DateTime.UtcNow.AddDays(15)
        );
        await eventStore.Create(futureVisibleEvent);
        var project = FakeData.ProjectFaker.Generate() with { EventId = futureVisibleEvent.Id };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Contains(AllProjects(projectList!), p => p.Title == project.Title);
    }

    [Fact]
    public async Task ProjectsWithPastVisibleFromAreVisibleToAttendees()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var visibleEvent = new Event(
            "visible-event",
            "Visible Event",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(44)),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(5)
        );
        await eventStore.Create(visibleEvent);
        var project = FakeData.ProjectFaker.Generate() with { EventId = visibleEvent.Id };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient().AuthenticateAsProjectAttendee("1234");

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Contains(AllProjects(projectList!), p => p.Title == project.Title);
    }

    [Fact]
    public async Task ProjectWithFutureRegistrationFromShowsRegistrationFromInsteadOfRegisterLink()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var registrationFutureEvent = new Event(
            "registration-future-event",
            "Registration Not Open",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(44)),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(5)
        );
        await eventStore.Create(registrationFutureEvent);
        var project = FakeData.ProjectFaker.Generate()
            with { EventId = registrationFutureEvent.Id, AllAttendees = Array.Empty<ProjectAttendee>() };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient().AuthenticateAsProjectAttendee("1234");

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        var eventGroup = Assert.Single(projectList!.Events, e => e.Projects.Any(p => p.Title == project.Title));
        var projectDto = Assert.Single(eventGroup.Projects, p => p.Title == project.Title);
        Assert.Null(projectDto.Links.Register);
        Assert.NotNull(eventGroup.RegistrationFromLocalUserTime);
    }
}
