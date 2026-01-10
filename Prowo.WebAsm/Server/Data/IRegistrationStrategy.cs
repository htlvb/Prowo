namespace Prowo.WebAsm.Server.Data;

public record ProjectRegistrationActions(bool CanRegister, bool CanDeregister);

public interface IRegistrationStrategy
{
    Dictionary<Project, ProjectRegistrationActions> GetRegistrationActions(ProjectAttendee attendee, IReadOnlyCollection<Project> projects);
}

public class FreeRegistrationStrategy : IRegistrationStrategy
{
    public Dictionary<Project, ProjectRegistrationActions> GetRegistrationActions(ProjectAttendee attendee, IReadOnlyCollection<Project> projects)
    {
        return projects.ToDictionary(v => v, _ => new ProjectRegistrationActions(true, true));
    }
}

public class IrrevocableRegistrationStrategy : IRegistrationStrategy
{
    public Dictionary<Project, ProjectRegistrationActions> GetRegistrationActions(ProjectAttendee attendee, IReadOnlyCollection<Project> projects)
    {
        return projects.ToDictionary(v => v, _ => new ProjectRegistrationActions(true, false));
    }
}

public class SingleRegistrationPerDayStrategy : IRegistrationStrategy
{
    public Dictionary<Project, ProjectRegistrationActions> GetRegistrationActions(ProjectAttendee attendee, IReadOnlyCollection<Project> projects)
    {
        return projects.ToDictionary(
            v => v,
            project =>
            {
                var canRegister = projects
                    .Where(p => p.Date == project.Date && p.Id != project.Id)
                    .All(p => !p.AllAttendees.Select(v => v.Id).Contains(attendee.Id));
                return new ProjectRegistrationActions(canRegister, true);
            });
    }
}

public class NoWaitingListStrategy : IRegistrationStrategy
{
    public Dictionary<Project, ProjectRegistrationActions> GetRegistrationActions(ProjectAttendee attendee, IReadOnlyCollection<Project> projects)
    {
        return projects.ToDictionary(
            v => v,
            project =>
            {
                var canRegister = project.MaxAttendees > project.AllAttendees.Count;
                return new ProjectRegistrationActions(canRegister, true);
            });
    }
}

public class NoRegistrationAfterClosingDateStrategy : IRegistrationStrategy
{
    public Dictionary<Project, ProjectRegistrationActions> GetRegistrationActions(ProjectAttendee attendee, IReadOnlyCollection<Project> projects)
    {
        return projects.ToDictionary(
            v => v,
            v =>
            {
                var canRegister = v.ClosingDate > DateTime.UtcNow;
                var canDeregister = v.ClosingDate > DateTime.UtcNow;
                return new ProjectRegistrationActions(canRegister, canDeregister);
            });
    }
}

public class NoRegistrationIfRegisteredStrategy : IRegistrationStrategy
{
    public Dictionary<Project, ProjectRegistrationActions> GetRegistrationActions(ProjectAttendee attendee, IReadOnlyCollection<Project> projects)
    {
        return projects.ToDictionary(
            v => v,
            v =>
            {
                var canRegister = v.GetUserRole(attendee.Id)
                    is not UserRoleForProject.Registered
                    and not UserRoleForProject.Waiting;
                var canDeregister = v.GetUserRole(attendee.Id)
                    is UserRoleForProject.Registered
                    or UserRoleForProject.Waiting;
                return new ProjectRegistrationActions(canRegister, canDeregister);
            });
    }
}

public class LogicalAndCombinationStrategy(IReadOnlyList<IRegistrationStrategy> strategies) : IRegistrationStrategy
{
    public Dictionary<Project, ProjectRegistrationActions> GetRegistrationActions(ProjectAttendee attendee, IReadOnlyCollection<Project> projects)
    {
        return strategies
            .Select(v => v.GetRegistrationActions(attendee, projects))
            .Aggregate((a, b) =>
            {
                foreach (var pair in b)
                {
                    var registrationActions = a.TryGetValue(pair.Key, out var actions)
                        ? new ProjectRegistrationActions(pair.Value.CanRegister && actions.CanRegister, pair.Value.CanDeregister && actions.CanDeregister)
                        : pair.Value;
                    a[pair.Key] = registrationActions;
                }
                return a;
            });
    }
}
