using Prowo.WebAsm.Server.Data;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class InMemoryUserStore : IUserStore
{
    public IAsyncEnumerable<ProjectAttendee> GetAttendeeCandidates()
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<ProjectOrganizer> GetOrganizerCandidates()
    {
        throw new NotImplementedException();
    }

    public Task<ProjectAttendee> GetSelfAsProjectAttendee()
    {
        throw new NotImplementedException();
    }
}
