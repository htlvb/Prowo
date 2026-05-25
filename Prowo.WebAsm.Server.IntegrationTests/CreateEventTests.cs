using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
using Prowo.WebAsm.Shared;
using System.Net;
using System.Net.Http.Json;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class CreateEventTests
{
    private static EditingEventDataDto ValidEventData() => new(
        "Projektwoche 2027",
        new DateOnly(2027, 7, 5),
        new DateOnly(2027, 7, 9),
        new DateTime(2027, 6, 1, 23, 59, 59, DateTimeKind.Unspecified),
        new DateTime(2027, 6, 15, 23, 59, 59, DateTimeKind.Unspecified)
    );

    [Fact]
    public async Task CanCreateEventAsAdmin()
    {
        using var host = await InMemoryServer.Start();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var data = ValidEventData();
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        using var response = await client.PostAsJsonAsync("/api/events", data, host.GetJsonSerializerOptions());

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected {HttpStatusCode.OK} but got {response.StatusCode}. Body: {responseBody}");
        var allEvents = await eventStore.GetAll().ToList();
        Assert.Contains(allEvents, e => e.Title == data.Title);
    }

    [Fact]
    public async Task CantCreateEventAsProjectWriter()
    {
        using var host = await InMemoryServer.Start();
        var data = ValidEventData();
        var writerId = FakeData.ProjectOrganizers.First().Id;
        using var client = host.GetTestClient().AuthenticateAsProjectWriter(writerId);

        using var response = await client.PostAsJsonAsync("/api/events", data, host.GetJsonSerializerOptions());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CantCreateEventAsProjectAttendee()
    {
        using var host = await InMemoryServer.Start();
        var data = ValidEventData();
        using var client = host.GetTestClient().AuthenticateAsProjectAttendee("attendee-1");

        using var response = await client.PostAsJsonAsync("/api/events", data, host.GetJsonSerializerOptions());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CantCreateEventWithEmptyTitle()
    {
        using var host = await InMemoryServer.Start();
        var data = ValidEventData() with { Title = "" };
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        using var response = await client.PostAsJsonAsync("/api/events", data, host.GetJsonSerializerOptions());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CantCreateEventWhenStartIsAfterEnd()
    {
        using var host = await InMemoryServer.Start();
        var data = ValidEventData() with { Start = new DateOnly(2027, 7, 9), End = new DateOnly(2027, 7, 5) };
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        using var response = await client.PostAsJsonAsync("/api/events", data, host.GetJsonSerializerOptions());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CantCreateEventWhenVisibleFromIsAfterRegistrationFrom()
    {
        using var host = await InMemoryServer.Start();
        var data = ValidEventData() with
        {
            VisibleFromLocalUserTime = new DateTime(2027, 6, 20, 23, 59, 59, DateTimeKind.Unspecified),
            RegistrationFromLocalUserTime = new DateTime(2027, 6, 15, 23, 59, 59, DateTimeKind.Unspecified)
        };
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        using var response = await client.PostAsJsonAsync("/api/events", data, host.GetJsonSerializerOptions());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CantCreateEventWhenRegistrationFromIsAfterEventStart()
    {
        using var host = await InMemoryServer.Start();
        var data = ValidEventData() with
        {
            Start = new DateOnly(2027, 7, 5),
            RegistrationFromLocalUserTime = new DateTime(2027, 7, 5, 12, 0, 0, DateTimeKind.Unspecified)
        };
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        using var response = await client.PostAsJsonAsync("/api/events", data, host.GetJsonSerializerOptions());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CanUpdateEventAsAdmin()
    {
        using var host = await InMemoryServer.Start();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var existing = FakeData.DefaultEvent;
        var updatedData = ValidEventData() with { Title = "Projektwoche Updated" };
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        using var response = await client.PostAsJsonAsync($"/api/events/{existing.Id}", updatedData, host.GetJsonSerializerOptions());

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected {HttpStatusCode.OK} but got {response.StatusCode}. Body: {responseBody}");
        var updated = await eventStore.Get(existing.Id);
        Assert.Equal("Projektwoche Updated", updated!.Title);
    }

    [Fact]
    public async Task CantUpdateEventAsProjectWriter()
    {
        using var host = await InMemoryServer.Start();
        var existing = FakeData.DefaultEvent;
        var updatedData = ValidEventData() with { Title = "Should Not Update" };
        var writerId = FakeData.ProjectOrganizers.First().Id;
        using var client = host.GetTestClient().AuthenticateAsProjectWriter(writerId);

        using var response = await client.PostAsJsonAsync($"/api/events/{existing.Id}", updatedData, host.GetJsonSerializerOptions());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CanDeleteEventAsAdmin()
    {
        using var host = await InMemoryServer.Start();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var newEvent = new Event("deletable-event", "To Delete", new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 5), DateTime.MinValue, DateTime.MinValue);
        await eventStore.Create(newEvent);
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        using var response = await client.DeleteAsync($"/api/events/{newEvent.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await eventStore.Get(newEvent.Id));
    }

    [Fact]
    public async Task CantDeleteEventWithProjects()
    {
        using var host = await InMemoryServer.Start();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var newEvent = new Event("event-with-projects", "Has Projects", new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 5), DateTime.MinValue, DateTime.MinValue);
        await eventStore.Create(newEvent);
        var project = FakeData.ProjectFaker.Generate() with { EventId = newEvent.Id };
        await projectStore.CreateProject(project);
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        using var response = await client.DeleteAsync($"/api/events/{newEvent.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CantDeleteEventAsProjectWriter()
    {
        using var host = await InMemoryServer.Start();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var newEvent = new Event("deletable-event-2", "To Delete", new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 5), DateTime.MinValue, DateTime.MinValue);
        await eventStore.Create(newEvent);
        var writerId = FakeData.ProjectOrganizers.First().Id;
        using var client = host.GetTestClient().AuthenticateAsProjectWriter(writerId);

        using var response = await client.DeleteAsync($"/api/events/{newEvent.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EventListContainsManageEventsLinkForAdmin()
    {
        using var host = await InMemoryServer.Start();
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter("admin-1");

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.NotNull(projectList!.Links.ManageEvents);
    }

    [Fact]
    public async Task EventListDoesNotContainManageEventsLinkForProjectWriter()
    {
        using var host = await InMemoryServer.Start();
        var writerId = FakeData.ProjectOrganizers.First().Id;
        using var client = host.GetTestClient().AuthenticateAsProjectWriter(writerId);

        var projectList = await client.GetFromJsonAsync<ProjectListDto>("/api/projects", host.GetJsonSerializerOptions());

        Assert.Null(projectList!.Links.ManageEvents);
    }
}
