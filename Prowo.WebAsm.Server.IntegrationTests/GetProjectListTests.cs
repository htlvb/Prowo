using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Json;
using Prowo.WebAsm.Shared;

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
}
