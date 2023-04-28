using Prowo.WebAsm.Server.Data;

namespace Prowo.WebAsm.Server.IntegrationTests.Utils;

public class InMemoryUserStore : IUserStore
{
    private readonly IReadOnlyCollection<ProjectOrganizer> organizerCandidates;

    public InMemoryUserStore(IReadOnlyCollection<ProjectOrganizer> organizerCandidates)
    {
        this.organizerCandidates = organizerCandidates;
    }

    public IAsyncEnumerable<ProjectAttendee> GetAttendeeCandidates()
    {
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<ProjectOrganizer> GetOrganizerCandidates()
    {
        foreach (var item in organizerCandidates)
        {
            await Task.Yield();
            yield return item;
        }
    }

    public Task<ProjectAttendee> GetSelfAsProjectAttendee()
    {
        throw new NotImplementedException();
    }
}
