using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;
using Prowo.WebAsm.Shared;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class DuplicateProjectTests
{
    [Fact]
    public async Task CanDuplicateOwnProject()
    {
        using var host = await InMemoryServer.Start();
        var project = FakeData.ProjectFaker.Generate();
        using var client = host.GetTestClient().AuthenticateAsProjectWriter(project.Organizer.Id);
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(project);
        
        var duplicatedProject = await client.GetFromJsonAsync<EditingProjectDto>($"/api/projects/edit/{project.Id}?duplicate=true", host.GetJsonSerializerOptions());

        Assert.NotNull(duplicatedProject);
        Assert.Equal(project.Organizer.Id, duplicatedProject.Data.OrganizerId);
    }
    
    [Fact]
    public async Task CanDuplicateOwnProjectFromPast()
    {
        using var host = await InMemoryServer.Start();
        var project = FakeData.PastProjectFaker.Generate();
        using var client = host.GetTestClient().AuthenticateAsProjectWriter(project.Organizer.Id);
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(project);
        
        var duplicatedProject = await client.GetFromJsonAsync<EditingProjectDto>($"/api/projects/edit/{project.Id}?duplicate=true", host.GetJsonSerializerOptions());

        Assert.NotNull(duplicatedProject);
        Assert.Equal(project.Organizer.Id, duplicatedProject.Data.OrganizerId);
    }

    [Fact]
    public async Task CanDuplicateOtherProject()
    {
        using var host = await InMemoryServer.Start();
        var project = FakeData.ProjectFaker.Generate();
        var user = FakeData.ProjectOrganizers.Except([project.Organizer]).First();
        using var client = host.GetTestClient().AuthenticateAsProjectWriter(user.Id);
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(project);
        
        var duplicatedProject = await client.GetFromJsonAsync<EditingProjectDto>($"/api/projects/edit/{project.Id}?duplicate=true", host.GetJsonSerializerOptions());

        Assert.NotNull(duplicatedProject);
        Assert.Equal(user.Id, duplicatedProject.Data.OrganizerId);
    }
    
    [Fact]
    public async Task CanDuplicateOtherProjectAsAllProjectsWriter()
    {
        using var host = await InMemoryServer.Start();
        var project = FakeData.ProjectFaker.Generate();
        var user = FakeData.ProjectOrganizers.Except([project.Organizer]).First();
        using var client = host.GetTestClient().AuthenticateAsAllProjectWriter(user.Id);
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(project);
        
        var duplicatedProject = await client.GetFromJsonAsync<EditingProjectDto>($"/api/projects/edit/{project.Id}?duplicate=true", host.GetJsonSerializerOptions());

        Assert.NotNull(duplicatedProject);
        Assert.Equal(project.Organizer.Id, duplicatedProject.Data.OrganizerId);
    }
    
    [Fact]
    public async Task SaveUrlDiffersWhenDuplicatingProject()
    {
        using var host = await InMemoryServer.Start();
        var project = FakeData.ProjectFaker.Generate();
        using var client = host.GetTestClient().AuthenticateAsProjectWriter(project.Organizer.Id);
        var projectStore = host.Services.GetRequiredService<IProjectStore>();
        await projectStore.CreateProject(project);
        
        var originalProject = await client.GetFromJsonAsync<EditingProjectDto>($"/api/projects/edit/{project.Id}", host.GetJsonSerializerOptions());
        var duplicatedProject = await client.GetFromJsonAsync<EditingProjectDto>($"/api/projects/edit/{project.Id}?duplicate=true", host.GetJsonSerializerOptions());

        Assert.NotNull(originalProject);
        Assert.NotNull(duplicatedProject);
        Assert.NotEqual(originalProject.Links.Save, duplicatedProject.Links.Save);
    }
}