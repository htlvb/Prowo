namespace Prowo.WebAsm.Server.Data
{
    public record Project(
        string Id,
        string Title,
        string Description,
        string Location,
        ProjectOrganizer Organizer,
        IReadOnlyList<ProjectOrganizer> CoOrganizers,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly? EndTime,
        DateTime ClosingDate,
        int MaxAttendees,
        IReadOnlyList<ProjectAttendee> AllAttendees
    )
    {
        public IEnumerable<ProjectAttendee> RegisteredAttendees => AllAttendees.Take(MaxAttendees);
        public IEnumerable<ProjectAttendee> WaitingAttendees => AllAttendees.Skip(MaxAttendees);
    }

    public record ProjectAttendee(
        string Id,
        string FirstName,
        string LastName,
        string Class
    );
}
