using System.Net;
using FsCheck.Xunit;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
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
}
