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

    public async Task<Project?> Get(string projectId)
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

    public async Task<Project> AddAttendee(string projectId, ProjectAttendee attendee)
    {
        await Task.Yield();
        int index = projects.FindIndex(v => v.Id == projectId);
        if (index < 0)
        {
            throw new Exception("Project not found");
        }
        projects[index] = projects[index] with
        {
            AllAttendees = projects[index].AllAttendees.Append(attendee).ToList()
        };
        return projects[index];
    }

    public async Task<Project> RemoveAttendee(string projectId, string userId)
    {
        await Task.Yield();
        int index = projects.FindIndex(v => v.Id == projectId);
        if (index < 0)
        {
            throw new Exception("Project not found");
        }
        projects[index] = projects[index] with
        {
            AllAttendees = projects[index].AllAttendees.Where(v => v.Id != userId).ToList()
        };
        return projects[index];
    }
}