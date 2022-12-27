using Prowo.WebAsm.Server.Data;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class InMemoryProjectStore : IProjectStore
{
    public IAsyncEnumerable<Project> GetAllSince(DateTime timestamp)
    {
        throw new NotImplementedException();
    }

    public Task<Project> Get(string projectId)
    {
        throw new NotImplementedException();
    }

    public Task CreateProject(Project project)
    {
        throw new NotImplementedException();
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