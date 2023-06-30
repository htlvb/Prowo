using FsCheck;
using FsCheck.Fluent;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Shared;
using System.Net.Mail;

namespace Prowo.WebAsm.Server.IntegrationTests;

public static class CustomGenerators
{
    public static Arbitrary<ProjectOrganizer> ProjectOrganizerGenerator()
    {
        var gen =
            from id in ArbMap.Default.GeneratorFor<Guid>()
            from firstName in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from lastName in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from shortName in ArbMap.Default.GeneratorFor<NonEmptyString>()
            select new ProjectOrganizer(id.ToString(), firstName.Get, lastName.Get, shortName.Get);
        return Arb.From(gen);
    }

    public static Arbitrary<DateOnly> ProjectDateGenerator()
    {
        var gen =
            from offset in Gen.Choose(-100, 100)
            let date = DateTime.Today.AddDays(offset)
            select DateOnly.FromDateTime(date);
        return Arb.From(gen);
    }

    public record StartTime(TimeOnly Time);

    public static Arbitrary<StartTime> StartTimeGenerator()
    {
        var gen =
            from hour in Gen.Choose(5, 22)
            from quarter in Gen.Choose(0, 3)
            let time = new TimeOnly(hour, quarter * 15)
            select new StartTime(time);
        return Arb.From(gen);
    }

    public static Arbitrary<TimeOnly?> EndTimeGenerator(StartTime startTime)
    {
        var maxDurationQuarters = 24 * 4 - 1 - startTime.Time.Hour * 4 - startTime.Time.Minute / 15;
        var gen = Gen.OneOf(
            Gen.Constant<TimeOnly?>(default),

            from durationQuarters in Gen.Choose(1, maxDurationQuarters)
            let time = startTime.Time.Add(TimeSpan.FromMinutes(15 * durationQuarters))
            select (TimeOnly?)time
        );
        return Arb.From(gen);
    }

    public record ClosingDate(DateTime DateTime);

    public static Arbitrary<ClosingDate> ClosingDateGenerator(DateOnly projectDate)
    {
        var daysToProject = Math.Abs(projectDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber);
        var gen =
            from offsetMinutes in Gen.Choose(0, 2 * daysToProject * 24 * 60)
            let date = projectDate.ToDateTime(TimeOnly.MinValue).AddMinutes(-offsetMinutes)
            select new ClosingDate(date);
        return Arb.From(gen);
    }

    public static Arbitrary<ProjectAttendee> ProjectAttendeeGenerator()
    {
        var gen =
            from id in ArbMap.Default.GeneratorFor<Guid>()
            from firstName in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from lastName in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from className in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from mailAddress in ArbMap.Default.GeneratorFor<MailAddress>()
            select new ProjectAttendee(id.ToString(), firstName.Get, lastName.Get, className.Get, mailAddress.ToString());
        return Arb.From(gen);
    }

    public static Arbitrary<ProjectAttendee[]> ProjectAttendeesGenerator(int maxAttendees)
    {
        var gen = Gen.OneOf(
            Gen.Constant(Array.Empty<ProjectAttendee>()),
                
            from count in Gen.Choose(1, maxAttendees - 1)
            from attendees in ProjectAttendeeGenerator().Generator.ArrayOf(count)
            select attendees,
                
            ProjectAttendeeGenerator().Generator.ArrayOf(maxAttendees),
                
            from count in Gen.Choose(maxAttendees + 1, 3 * maxAttendees)
            from attendees in ProjectAttendeeGenerator().Generator.ArrayOf(count)
            select attendees
        );
        return Arb.From(gen);
    }

    public static Arbitrary<Project> ProjectGenerator()
    {
        var gen =
            from id in ArbMap.Default.GeneratorFor<Guid>()
            from title in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from description in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from location in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from organizer in ProjectOrganizerGenerator().Generator
            from coOrganizers in ProjectOrganizerGenerator().Array().Generator
            from date in ProjectDateGenerator().Generator
            from startTime in StartTimeGenerator().Generator
            from endTime in EndTimeGenerator(startTime).Generator
            from closingDate in ClosingDateGenerator(date).Generator
            from maxAttendees in Gen.Choose(1, 1000)
            from allAttendees in ProjectAttendeesGenerator(maxAttendees).Generator
            select new Project(
                id.ToString(),
                title.Get,
                description.Get,
                location.Get,
                organizer,
                coOrganizers,
                date,
                startTime.Time,
                endTime,
                closingDate.DateTime,
                maxAttendees,
                allAttendees
            );
        return Arb.From(gen);
    }

    public record AttendableProject(Project Project);

    public static Arbitrary<AttendableProject> AttendableProjectGenerator()
    {
        var gen =
            from project in ProjectGenerator().Generator
            where project.ClosingDate > DateTime.UtcNow
            select new AttendableProject(project);
        return Arb.From(gen);
    }

    public record AttendableProjectWithAttendees(Project Project);

    public static Arbitrary<AttendableProjectWithAttendees> AttendableProjectWithAttendeesGenerator()
    {
        var gen =
            from p in AttendableProjectGenerator().Generator
            let project = p.Project
            where project.AllAttendees.Count > 0
            select new AttendableProjectWithAttendees(project);
        return Arb.From(gen);
    }

    public record UnattendableProjectWithAttendees(Project Project);

    public static Arbitrary<UnattendableProjectWithAttendees> UnattendableProjectWithAttendeesGenerator()
    {
        var gen =
            from project in ProjectGenerator().Generator
            where project.Date > DateOnly.FromDateTime(DateTime.Today)
            where project.ClosingDate < DateTime.Today
            where project.AllAttendees.Count > 0
            select new UnattendableProjectWithAttendees(project);
        return Arb.From(gen);
    }

    public record FutureProject(Project Project);

    public static Arbitrary<FutureProject> FutureProjectGenerator()
    {
        var gen =
            from project in ProjectGenerator().Generator
            where project.Date > DateOnly.FromDateTime(DateTime.Today)
            select new FutureProject(project);
        return Arb.From(gen);
    }

    public record FutureProjectWithAttendees(Project Project);

    public static Arbitrary<FutureProjectWithAttendees> FutureProjectWithAttendeesGenerator()
    {
        var gen =
            from p in FutureProjectGenerator().Generator
            let project = p.Project
            where project.AllAttendees.Count > 0
            select new FutureProjectWithAttendees(project);
        return Arb.From(gen);
    }

    public static Arbitrary<EditingProjectDataDto> EditingProjectDataDtoGenerator()
    {
        var gen =
            from title in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from description in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from location in ArbMap.Default.GeneratorFor<NonEmptyString>()
            from organizerId in ArbMap.Default.GeneratorFor<Guid>()
            from coOrganizerIds in ArbMap.Default.ArbFor<Guid>().Array().Generator
            from date in ProjectDateGenerator().Generator
            from startTime in StartTimeGenerator().Generator
            from endTime in EndTimeGenerator(startTime).Generator
            from closingDate in ClosingDateGenerator(date).Generator
            from maxAttendees in Gen.Choose(1, 1000)
            select new EditingProjectDataDto(
                title.Get,
                description.Get,
                location.Get,
                organizerId.ToString(),
                coOrganizerIds.Select(v => v.ToString()).ToArray(),
                date,
                startTime.Time,
                endTime,
                closingDate.DateTime,
                maxAttendees
            );
        return Arb.From(gen);
    }

    public record ValidEditingProjectDataDto(EditingProjectDataDto Project);

    public static Arbitrary<ValidEditingProjectDataDto> ValidEditingProjectDataDtoGenerator()
    {
        var gen =
            from project in EditingProjectDataDtoGenerator().Generator
            where project.ClosingDate > DateTime.Today
            select new ValidEditingProjectDataDto(project);
        return Arb.From(gen);
    }

    //public static Arbitrary<TimeOnly> TimeOnlyGenerator()
    //{
    //    var gen =
    //        from h in Gen.Choose(0, 23)
    //        from min in Gen.Choose(0, 59)
    //        from sec in Gen.Choose(0, 59)
    //        from ms in Gen.Choose(0, 999)
    //        select new TimeOnly(h, min, sec, ms);
    //    var shrink = (TimeOnly t) =>
    //    {
    //        if (t.Hour != 0) return new[] { new TimeOnly(0, t.Minute, t.Second, t.Millisecond) };
    //        if (t.Minute != 0) return new[] { new TimeOnly(0, 0, t.Second, t.Millisecond) };
    //        if (t.Second != 0) return new[] { new TimeOnly(0, 0, 0, t.Millisecond) };
    //        if (t.Millisecond != 0) return new[] { System.TimeOnly.MinValue };
    //        return Array.Empty<TimeOnly>();
    //    };
    //    return Arb.From(gen, shrink);
    //}
}
