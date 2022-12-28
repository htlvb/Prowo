using Prowo.WebAsm.Shared;

namespace Prowo.WebAsm.Server.Data
{
    public sealed record Project(
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

        public static Project FromEditingProjectDataDto(
            EditingProjectDataDto projectData,
            string projectId,
            IReadOnlyDictionary<string, ProjectOrganizer> organizerCandidates
        )
        {
            var organizer =
                organizerCandidates.TryGetValue(projectData.OrganizerId, out ProjectOrganizer? projectOrganizer)
                    ? projectOrganizer
                    : throw new Exception($"Organizer with ID \"{projectData.OrganizerId}\" not found");
            var coOrganizers = projectData.CoOrganizerIds
                .Except(new[] { projectData.OrganizerId })
                .Select(coOrganizerId =>
                    organizerCandidates.TryGetValue(coOrganizerId, out ProjectOrganizer? projectCoOrganizer)
                        ? projectCoOrganizer
                        : throw new Exception($"Co-Organizer with ID \"{coOrganizerId}\" not found")
                )
                .ToArray();
            return new Project(
                projectId,
                projectData.Title,
                projectData.Description,
                projectData.Location,
                organizer,
                coOrganizers,
                projectData.Date,
                projectData.StartTime,
                projectData.EndTime,
                projectData.ClosingDate.FromUserTime(),
                projectData.MaxAttendees,
                Array.Empty<ProjectAttendee>()
            );
        }
    }

    public record ProjectAttendee(
        string Id,
        string FirstName,
        string LastName,
        string Class
    );
}
