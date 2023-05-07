using FsCheck.Fluent;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
using System.Net;
using System.Net.Http.Json;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class AuthorizationTests
{
    public static IEnumerable<object?[]> ResourcesWithAuthorization()
    {
        yield return new object?[]
        {
            Enumerable.Empty<Project>(),
            HttpMethod.Get,
            "/api/projects",
            default(HttpContent?)
        };
        foreach (var p in CustomGenerators.AttendableProjectGenerator().Generator.Sample(10))
        {
            yield return new object?[]
            {
                new[] { p.Project },
                HttpMethod.Post,
                $"/api/projects/{p.Project.Id}/register",
                default(HttpContent?)
            };
        }
        foreach (var p in CustomGenerators.AttendableProjectGenerator().Generator.Sample(10))
        {
            yield return new object?[]
            {
                new[] { p.Project },
                HttpMethod.Post,
                $"/api/projects/{p.Project.Id}/deregister",
                default(HttpContent?)
            };
        }
        foreach (var p in CustomGenerators.AttendableProjectWithAttendeesGenerator().Generator.Sample(10))
        {
            foreach (var attendee in Gen.Elements(p.Project.AllAttendees.AsEnumerable()).Sample(10))
            {
                yield return new object?[]
                {
                    new[] { p.Project },
                    HttpMethod.Delete,
                    $"/api/projects/{p.Project.Id}/attendees/{attendee.Id}",
                    default(HttpContent?)
                };
            }
        }
        foreach (var p in CustomGenerators.AttendableProjectGenerator().Generator.Sample(10))
        {
            yield return new object?[]
            {
                new[] { p.Project },
                HttpMethod.Get,
                $"/api/projects/edit/{p.Project.Id}",
                default(HttpContent?)
            };
        }
        yield return new object?[]
        {
            Enumerable.Empty<Project>(),
            HttpMethod.Get,
            $"/api/projects/edit/new",
            default(HttpContent?)

        };
        foreach (var project in CustomGenerators.EditingProjectDataDtoGenerator().Generator.Sample(10))
        {
            yield return new object?[]
            {
                Enumerable.Empty<Project>(),
                HttpMethod.Post,
                "/api/projects",
                JsonContent.Create(project)
            };
        }
        foreach (var p in CustomGenerators.AttendableProjectGenerator().Generator.Sample(10))
        {
            foreach (var update in CustomGenerators.EditingProjectDataDtoGenerator().Generator.Sample(10))
            {
                yield return new object?[]
                {
                    new[] { p.Project },
                    HttpMethod.Post,
                    $"/api/projects/{p.Project.Id}",
                    JsonContent.Create(update)
                };
            }
        }
        foreach (var p in CustomGenerators.AttendableProjectGenerator().Generator.Sample(10))
        {
            yield return new object?[]
            {
                new[] { p.Project },
                HttpMethod.Delete,
                $"/api/projects/{p.Project.Id}",
                default(HttpContent?)
            };
        }
        foreach (var p in CustomGenerators.AttendableProjectGenerator().Generator.Sample(10))
        {
            yield return new object?[]
            {
                new[] { p.Project },
                HttpMethod.Get,
                $"/api/projects/{p.Project.Id}/attendees",
                default(HttpContent?)
            };
        }
        yield return new object?[]
        {
            Enumerable.Empty<Project>(),
            HttpMethod.Get,
            $"/api/projects/attendees",
            default(HttpContent?)
        };
    }

    [Theory]
    [MemberData(nameof(ResourcesWithAuthorization))]
    public async Task CantAccessProjectResourcesWhenNotAuthenticated(
        IEnumerable<Project> projects,
        HttpMethod httpMethod,
        string url,
        HttpContent httpContent)
    {
        // Arrange
        using var host = await InMemoryServer.Start();
        using var client = host.GetTestClient();
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        foreach (var project in projects)
        {
            await projectStore.CreateProject(project);
        }
        using var request = new HttpRequestMessage(httpMethod, url)
        {
            Content = httpContent
        };

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
