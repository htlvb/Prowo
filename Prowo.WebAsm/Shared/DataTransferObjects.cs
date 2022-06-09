namespace Prowo.WebAsm.Shared
{
    public record AttendanceOverviewDto(
        IReadOnlyList<DateOnly> Dates,
        IReadOnlyList<GroupDto> Groups
    );

    public record GroupDto(string Name, IReadOnlyList<StudentDto> Students);

    public record StudentDto(string FirstName, string LastName, IReadOnlyList<StudentProjectsAtDateDto> Projects);

    public record StudentProjectsAtDateDto(IReadOnlyList<StudentProjectDto> List);

    public record StudentProjectDto(string Name, bool IsWaiting);

    public record ProjectAttendeesDto(
        string Title,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly? EndTime,
        IReadOnlyList<ProjectAttendeeDto> Attendees
    );

    public record ProjectAttendeeDto(
        string FirstName,
        string LastName,
        string Class,
        bool IsWaiting
    );

    public record EditingProjectDto(
        EditingProjectDataDto Data,
        IReadOnlyList<ProjectOrganizerDto> OrganizerCandidates,
        IReadOnlyList<ProjectOrganizerDto> CoOrganizerCandidates,
        EditingProjectLinksDto Links
    );

    public record EditingProjectDataDto(
        string Title,
        string Description,
        string Location,
        string OrganizerId,
        IReadOnlyList<string> CoOrganizerIds,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly? EndTime,
        DateTime ClosingDate,
        int MaxAttendees
    );

    public record EditingProjectLinksDto(
        string? Save
    );

    public record ProjectListDto(
        IReadOnlyList<ProjectDto> Projects,
        ProjectListLinksDto Links
    );

    public record ProjectListLinksDto(
        string? ShowAllAttendees,
        string? CreateProject
    );

    public record ProjectDto(
        string Title,
        string Description,
        string Location,
        ProjectOrganizerDto Organizer,
        IReadOnlyList<ProjectOrganizerDto> CoOrganizers,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly? EndTime,
        DateTime ClosingDate,
        DateTime ClosingDateLocalUserTime,
        int Attendees,
        int MaxAttendees,
        RegistrationStatusDto CurrentUserRegistrationStatus,
        ProjectLinksDto Links
    )
    {
        public bool RegistrationDisabled => ClosingDate <= DateTime.UtcNow;
    }

    public record ProjectOrganizerDto(
        string Id,
        string DisplayName
    );

    public enum RegistrationStatusDto
    {
        NotRegistered,
        Registered,
        Waiting
    }

    public record ProjectLinksDto(
        string? Register,
        string? Deregister,
        string? Edit,
        string? ShowAttendees);
}
