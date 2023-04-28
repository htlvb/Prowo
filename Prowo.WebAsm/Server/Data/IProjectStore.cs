namespace Prowo.WebAsm.Server.Data
{
    public interface IProjectStore
    {
        IAsyncEnumerable<Project> GetAllSince(DateTime timestamp);
        Task<Project> Get(string projectId);
        Task Delete(string projectId);
        Task CreateProject(Project project);
        Task UpdateProject(Project project);
        Task<Project> AddAttendee(string projectId, ProjectAttendee attendee);
        Task<Project> RemoveAttendee(string projectId, string userId);
    }
}