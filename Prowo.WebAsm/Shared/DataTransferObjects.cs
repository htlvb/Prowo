using System.Text.Json.Serialization;

namespace Prowo.WebAsm.Shared
{
    public record AttendanceOverviewDto(
        IReadOnlyList<DateOnly> Dates,
        IReadOnlyList<GroupDto> Groups
    );

    public record GroupDto(string Name, IReadOnlyList<StudentDto> Students);

    public record StudentDto(string FirstName, string LastName, string MailAddress, IReadOnlyList<StudentProjectsAtDateDto> Projects);

    public record StudentProjectsAtDateDto(IReadOnlyList<StudentProjectDto> List);

    public record StudentProjectDto(string Name, string LongName, bool IsWaiting, string? ShowProjectAttendeesLink, string? UserRegistrationLink);

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
        string MailAddress,
        bool IsWaiting,
        string? RegistrationLink
    );

    public record EditingProjectDto(
        EditingProjectDataDto Data,
        IReadOnlyList<ProjectOrganizerDto> OrganizerCandidates,
        IReadOnlyList<ProjectOrganizerDto> CoOrganizerCandidates,
        IReadOnlyList<EventDto> AvailableEvents,
        EditingProjectLinksDto Links
    );

    public record ProjectPaymentDataDto(
        string Iban,
        string AccountHolder,
        decimal? Amount,
        string RemittanceInformation
    );

    public record ProjectPaymentInfoDto(
        string Iban,
        string AccountHolder,
        decimal? Amount,
        string RemittanceInformation,
        string QrCodeBase64Png
    );

    public record EditingProjectDataDto(
        string? EventId,
        string Title,
        string Description,
        string Location,
        string OrganizerId,
        IReadOnlyList<string> CoOrganizerIds,
        DateOnly? Date,
        TimeOnly? StartTime,
        TimeOnly? EndTime,
        DateTime? ClosingDate,
        int? MaxAttendees,
        bool HasPaymentInfo,
        ProjectPaymentDataDto? PaymentData
    );

    public record EditingProjectLinksDto(
        string? Save
    );

    public record EventWithProjectsDto(
        string Id,
        string Title,
        DateOnly Start,
        DateOnly End,
        DateTime? RegistrationFromLocalUserTime,
        IReadOnlyList<ProjectDto> Projects
    );

    public record ProjectListDto(
        IReadOnlyList<EventWithProjectsDto> Events,
        ProjectListLinksDto Links
    );

    public record ProjectListLinksDto(
        string? ShowAllAttendees,
        string? CreateProject,
        string? DuplicateProject,
        string? ManageEvents = null
    );

    public record EventDto(
        string Id,
        string Title,
        DateOnly Start,
        DateOnly End,
        DateTime VisibleFrom,
        DateTime RegistrationFrom
    );

    public record EventListDto(
        IReadOnlyList<EventDto> Events,
        EventListLinksDto Links
    );

    public record EventListLinksDto(string? Create);

    public record EditingEventDto(
        EditingEventDataDto Data,
        EditingEventLinksDto Links
    );

    public record EditingEventDataDto(
        string Title,
        DateOnly? Start,
        DateOnly? End,
        DateTime? VisibleFromLocalUserTime,
        DateTime? RegistrationFromLocalUserTime
    );

    public record EditingEventLinksDto(string? Save);

    public record ProjectDto(
        string Title,
        string Description,
        string Location,
        ProjectOrganizerDto Organizer,
        IReadOnlyList<ProjectOrganizerDto> CoOrganizers,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly? EndTime,
        DateTime ClosingDateLocalUserTime,
        int Attendees,
        int MaxAttendees,
        UserRoleForProjectDto CurrentUserStatus,
        ProjectLinksDto Links,
        ProjectPaymentInfoDto? PaymentInfo = null
    )
    {
        public bool IsUserProject =>
            CurrentUserStatus == UserRoleForProjectDto.Registered
            || CurrentUserStatus == UserRoleForProjectDto.Waiting
            || CurrentUserStatus == UserRoleForProjectDto.Organizer
            || CurrentUserStatus == UserRoleForProjectDto.CoOrganizer;
    }

    public record ProjectOrganizerDto(
        string Id,
        string DisplayName
    );

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserRoleForProjectDto
    {
        NotRelated,
        Registered,
        Waiting,
        Organizer,
        CoOrganizer
    }

    public record ProjectLinksDto(
        string? Register,
        string? Deregister,
        string? Edit,
        string? Delete,
        string? ShowAttendees
    );

    public record ProjectToDuplicateDto(
        string DuplicateLink,
        string Title,
        string OrganizerShortName,
        IReadOnlyCollection<string> CoOrganizerShortNames,
        DateOnly Date
    );
}
