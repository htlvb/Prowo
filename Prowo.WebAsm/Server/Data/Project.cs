using Prowo.WebAsm.Shared;
using System.Diagnostics.CodeAnalysis;

namespace Prowo.WebAsm.Server.Data
{
    public record ProjectPaymentInfo(
        string Iban,
        string AccountHolder,
        decimal? Amount,
        string RemittanceInformation
    );

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
        IReadOnlyList<ProjectAttendee> AllAttendees,
        ProjectPaymentInfo? PaymentInfo = null
    )
    {
        public IEnumerable<ProjectAttendee> RegisteredAttendees => AllAttendees.Take(MaxAttendees);
        public IEnumerable<ProjectAttendee> WaitingAttendees => AllAttendees.Skip(MaxAttendees);

        public UserRoleForProject GetUserRole(string userId)
        {
            if (Organizer.Id == userId)
            {
                return UserRoleForProject.Organizer;
            }
            if (CoOrganizers.Any(v => v.Id == userId))
            {
                return UserRoleForProject.CoOrganizer;
            }
            if (RegisteredAttendees.Any(v => v.Id == userId))
            {
                return UserRoleForProject.Registered;
            }
            if (WaitingAttendees.Any(v => v.Id == userId))
            {
                return UserRoleForProject.Waiting;
            }
            return UserRoleForProject.NotRelated;
        }

        public static bool TryCreateFromEditingProjectDataDto(
            EditingProjectDataDto projectData,
            string projectId,
            IReadOnlyDictionary<string, ProjectOrganizer> organizerCandidates,
            TimeProvider timeProvider,
            ProjectPaymentInfo? paymentInfo,
            [NotNullWhen(true)]out Project? project,
            [NotNullWhen(false)]out string? errorMessage
        )
        {
            if (string.IsNullOrWhiteSpace(projectData.Title))
            {
                project = null;
                errorMessage = "Project title must not be empty.";
                return false;
            }
            if (!organizerCandidates.TryGetValue(projectData.OrganizerId, out var organizer))
            {
                project = null;
                errorMessage = $"Organizer with ID \"{projectData.OrganizerId}\" not found";
                return false;
            }
            var coOrganizerIds = projectData.CoOrganizerIds.Except(new[] { projectData.OrganizerId });
            var coOrganizerErrors = coOrganizerIds
                .Where(coOrganizerId => !organizerCandidates.ContainsKey(coOrganizerId))
                .ToList();
            if (coOrganizerErrors.Count > 0)
            {
                project = null;
                errorMessage = $"Co-Organizers with ID(s) {string.Join(", ", coOrganizerErrors.Select(v => $"\"{v}\""))} not found";
                return false;
            }
            var coOrganizers = coOrganizerIds
                .Select(v => organizerCandidates[v])
                .ToList();
            if (projectData.Date == null)
            {
                project = null;
                errorMessage = "Project date must be set.";
                return false;
            }
            if (projectData.StartTime == null)
            {
                project = null;
                errorMessage = "Project start time must be set.";
                return false;
            }
            if (projectData.ClosingDate == null)
            {
                project = null;
                errorMessage = "Project closing date must be set.";
                return false;
            }
            if (projectData.MaxAttendees == null)
            {
                project = null;
                errorMessage = "Max attendees must be set.";
                return false;
            }
            if (projectData.Date.Value < DateOnly.FromDateTime(timeProvider.GetLocalNow().Date))
            {
                project = null;
                errorMessage = "Project date must be in the future.";
                return false;
            }
            if (projectData.EndTime != null && projectData.StartTime.Value > projectData.EndTime.Value)
            {
                project = null;
                errorMessage = "Project start and end times are invalid.";
                return false;
            }
            if (projectData.ClosingDate.Value < timeProvider.GetLocalNow().DateTime)
            {
                project = null;
                errorMessage = "Project closing date must be in the future.";
                return false;
            }
            if (projectData.ClosingDate.Value >= projectData.Date.Value.ToDateTime(TimeOnly.MinValue))
            {
                project = null;
                errorMessage = "Project closing date must be before the project date.";
                return false;
            }
            if (projectData.MaxAttendees.Value < 1)
            {
                project = null;
                errorMessage = "Max attendees must be greater than 0.";
                return false;
            }
            project = new Project(
                projectId,
                projectData.Title,
                projectData.Description,
                projectData.Location,
                organizer,
                coOrganizers,
                projectData.Date.Value,
                projectData.StartTime.Value,
                projectData.EndTime,
                projectData.ClosingDate.Value.FromUserTime(),
                projectData.MaxAttendees.Value,
                [],
                paymentInfo
            );
            errorMessage = null;
            return true;
        }
    }
    
    public enum UserRoleForProject
    {
        NotRelated,
        Registered,
        Waiting,
        Organizer,
        CoOrganizer
    }

    public record ProjectAttendee(
        string Id,
        string FirstName,
        string LastName,
        string Class,
        string MailAddress
    );
}
