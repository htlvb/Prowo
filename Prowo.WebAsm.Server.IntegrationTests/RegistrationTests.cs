using System.Net;
using FsCheck.Xunit;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
using Prowo.WebAsm.Shared;
using static Prowo.WebAsm.Server.IntegrationTests.CustomGenerators;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class RegistrationTests
{
    [Property(Arbitrary = new[] { typeof(CustomGenerators) })]
    public async Task CantRegisterForProjectWhenClosingDateIsInThePast(UnattendableProjectWithAttendees p)
    {
        // Arrange
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(p.Project);

        using var client = host.GetTestClient()
            .AuthenticateAsProjectAttendee("1234"); // TODO use real id from IUserStore?
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{p.Project.Id}/register");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Property(Arbitrary = new[] { typeof(CustomGenerators) })]
    public async Task CanRegisterForProjectWhenClosingDateIsInTheFuture(AttendableProjectWithAttendees p)
    {
        // Arrange
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(p.Project);

        using var client = host.GetTestClient()
            .AuthenticateAsProjectAttendee("1234"); // TODO use real id from IUserStore?
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{p.Project.Id}/register");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CantRegisterForProjectWhenRegistrationFromIsInTheFuture()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var registrationFutureEvent = new Event(
            "registration-future-reg",
            "Registration Not Open Yet",
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
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{project.Id}/register");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CanRegisterForProjectWhenRegistrationFromIsInThePast()
    {
        using var host = await InMemoryServer.Start();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        var eventStore = host.Services.GetRequiredService<IEventStore>();
        var registrationOpenEvent = new Event(
            "registration-open-reg",
            "Registration Open",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(44)),
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddDays(-1)
        );
        await eventStore.Create(registrationOpenEvent);
        var project = FakeData.ProjectFaker.Generate()
            with { EventId = registrationOpenEvent.Id, AllAttendees = Array.Empty<ProjectAttendee>() };
        await projectStore.CreateProject(project);

        using var client = host.GetTestClient().AuthenticateAsProjectAttendee("1234");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{project.Id}/register");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
