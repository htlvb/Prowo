namespace Prowo.WebAsm.Server.Data
{
    public interface IUserStore
    {
        IAsyncEnumerable<ProjectOrganizer> GetOrganizerCandidates();
        IAsyncEnumerable<ProjectAttendee> GetAttendeeCandidates();
        Task<ProjectAttendee> GetSelfAsProjectAttendee();
    }
}