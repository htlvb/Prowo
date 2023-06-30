using Prowo.WebAsm.Server.Data;

namespace Prowo.WebAsm.Server.IntegrationTests.Utils;

public class InMemoryUserStore : IUserStore
{
    private readonly IReadOnlyCollection<ProjectOrganizer> organizerCandidates;
    private readonly ProjectAttendee selfAsProjectAttendee;

    public InMemoryUserStore(
        IReadOnlyCollection<ProjectOrganizer> organizerCandidates,
        ProjectAttendee selfAsProjectAttendee)
    {
        this.organizerCandidates = organizerCandidates;
        this.selfAsProjectAttendee = selfAsProjectAttendee;
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

    public async Task<ProjectAttendee> GetSelfAsProjectAttendee()
    {
        await Task.Yield();
        return selfAsProjectAttendee;
    }
}
