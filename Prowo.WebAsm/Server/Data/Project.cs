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
            [NotNullWhen(false)]out string[]? errorMessages
        )
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(projectData.Title))
                errors.Add("Titel darf nicht leer sein.");

            organizerCandidates.TryGetValue(projectData.OrganizerId, out var organizer);
            if (organizer == null)
                errors.Add("Organisator nicht gefunden.");

            var coOrganizerIds = projectData.CoOrganizerIds.Except([projectData.OrganizerId]).ToList();
            if (coOrganizerIds.Any(id => !organizerCandidates.ContainsKey(id)))
                errors.Add("Einer oder mehrere Betreuer wurden nicht gefunden.");

            if (projectData.Date == null) errors.Add("Datum muss gesetzt werden.");
            if (projectData.StartTime == null) errors.Add("Startzeit muss gesetzt werden.");
            if (projectData.ClosingDate == null) errors.Add("Anmeldeschluss muss gesetzt werden.");
            if (projectData.MaxAttendees == null) errors.Add("Maximale Teilnehmerzahl muss gesetzt werden.");

            if (projectData.Date != null && projectData.Date.Value < DateOnly.FromDateTime(timeProvider.GetLocalNow().Date))
                errors.Add("Datum muss in der Zukunft liegen.");
            if (projectData.EndTime != null && projectData.StartTime != null && projectData.StartTime.Value > projectData.EndTime.Value)
                errors.Add("Startzeit muss vor der Endzeit liegen.");
            if (projectData.ClosingDate != null && projectData.ClosingDate.Value < timeProvider.GetLocalNow().DateTime)
                errors.Add("Anmeldeschluss muss in der Zukunft liegen.");
            if (projectData.Date != null && projectData.ClosingDate != null && projectData.ClosingDate.Value >= projectData.Date.Value.ToDateTime(TimeOnly.MinValue))
                errors.Add("Anmeldeschluss muss vor dem Projektdatum liegen.");
            if (projectData.MaxAttendees != null && projectData.MaxAttendees.Value < 1)
                errors.Add("Maximale Teilnehmerzahl muss mindestens 1 sein.");

            if (organizer == null ||
                projectData.Date == null ||
                projectData.StartTime == null ||
                projectData.ClosingDate == null ||
                projectData.MaxAttendees == null ||
                errors.Count > 0)
            {
                project = null;
                errorMessages = [..errors];
                return false;
            }

            var coOrganizers = coOrganizerIds.Select(v => organizerCandidates[v]).ToList();
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
            errorMessages = null;
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
