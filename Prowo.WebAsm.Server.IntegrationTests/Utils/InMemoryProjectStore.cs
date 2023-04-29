﻿using Prowo.WebAsm.Client.Pages;
using Prowo.WebAsm.Server.Data;

namespace Prowo.WebAsm.Server.IntegrationTests.Utils;

public class InMemoryProjectStore : IProjectStore
{
    private readonly List<Project> projects = new();

    public async IAsyncEnumerable<Project> GetAllSince(DateTime timestamp)
    {
        foreach (var project in projects.Where(v => v.Date.ToDateTime(TimeOnly.MinValue) >= timestamp))
        {
            await Task.Yield();
            yield return project;
        }
    }

    public async Task<Project> Get(string projectId)
    {
        await Task.Yield();
        return projects.Find(v => v.Id == projectId);
    }

    public async Task Delete(string projectId)
    {
        await Task.Yield();
        projects.RemoveAll(v => v.Id == projectId);
    }

    public async Task CreateProject(Project project)
    {
        await Task.Yield();
        projects.Add(project);
    }

    public Task UpdateProject(Project project)
    {
        throw new NotImplementedException();
    }

    public Task<Project> AddAttendee(string projectId, ProjectAttendee attendee)
    {
        throw new NotImplementedException();
    }

    public Task<Project> RemoveAttendee(string projectId, string userId)
    {
        throw new NotImplementedException();
    }
}