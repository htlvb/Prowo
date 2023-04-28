using Prowo.WebAsm.Server.Data;
using System.Diagnostics.CodeAnalysis;

namespace Prowo.WebAsm.Server.IntegrationTests.Utils;

public class ProjectEqualityComparer : IEqualityComparer<Project?>
{
    public bool Equals(Project? x, Project? y)
    {
        if (x == null && y == null) { return true; }
        if (x == null) { return false; }
        if (y == null) { return false; }
        return Equals(x.Id, y.Id) &&
            Equals(x.Title, y.Title) &&
            Equals(x.Description, y.Description) &&
            Equals(x.Location, y.Location) &&
            Equals(x.Organizer, y.Organizer) &&
            x.CoOrganizers.SequenceEqual(y.CoOrganizers) &&
            Equals(x.Date, y.Date) &&
            Equals(x.StartTime, y.StartTime) &&
            Equals(x.EndTime, y.EndTime) &&
            Equals(x.ClosingDate, y.ClosingDate) &&
            Equals(x.MaxAttendees, y.MaxAttendees) &&
            x.AllAttendees.SequenceEqual(y.AllAttendees);
    }

    public int GetHashCode([DisallowNull] Project obj)
    {
        throw new NotImplementedException();
    }
}
