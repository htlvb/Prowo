using System;
using System.Collections.Generic;

namespace Prowo.Web.Data
{
    public record Project(
        string Id,
        string Title,
        string Description,
        string Location,
        string OrganizerId,
        IReadOnlyList<string> CoOrganizerIds,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly? EndTime,
        int MaxAttendees,
        IReadOnlyList<ProjectAttendee> Attendees
    );
}
