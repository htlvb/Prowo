using Bogus;
using Prowo.WebAsm.Server;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Shared;

namespace Prowo.WebAsm.Server.IntegrationTests.Utils;

public static class FakeData
{
    public static Faker<Project> ProjectFaker { get; } = new Faker<Project>()
        .CustomInstantiator(v =>
        {
            var date = v.Date.SoonDateOnly(20);
            var organizers = ProjectOrganizers
                .OrderBy(_ => v.Random.Double())
                .Take(v.Random.Number(1, 5))
                .ToList();
            var maxAttendees = v.Random.Number(1, 500);
            var attendeeCount = v.Random.Number(1, 3) switch
            {
                1 => 0,
                2 => maxAttendees,
                _ => v.Random.Number(1, maxAttendees - 1)
            };
            var attendees = ProjectAttendees
                .OrderBy(_ => v.Random.Double())
                .Take(attendeeCount)
                .ToList();
            return new Project(
                v.Random.Uuid().ToString(),
                v.Random.Words(),
                v.Lorem.Sentences(),
                v.Address.BuildingNumber(),
                organizers.First(),
                organizers.Skip(1).ToList(),
                date,
                new TimeOnly(7, 0).AddMinutes(v.Random.Number(0, 8) * 15),
                v.Random.Bool() ? new TimeOnly(12, 0).AddMinutes(v.Random.Number(0, 12) * 15) : null,
                new DateTime(v.Date.Between(v.Date.Soon(5), date.ToDateTime(TimeOnly.MinValue)).Ticks, DateTimeKind.Unspecified),
                v.Random.Number(1, 500),
                attendees
            );
        });

    public static Faker<Project> PastProjectFaker { get; } = new Faker<Project>()
        .CustomInstantiator(v =>
        {
            var date = v.Date.RecentDateOnly(20, DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
            var closingDate = new DateTime(v.Date.Between(DateTime.Today.AddDays(-40), date.ToDateTime(TimeOnly.MinValue)).Ticks, DateTimeKind.Unspecified);
            return ProjectFaker.Generate() with { Date = date, ClosingDate = closingDate };
        });

    public static Faker<EditingProjectDataDto> EditingProjectDataDtoFaker { get; } = new Faker<EditingProjectDataDto>()
        .CustomInstantiator(v =>
        {
            var date = v.Date.SoonDateOnly(20);
            var organizerIds = ProjectOrganizers
                .OrderBy(_ => v.Random.Double())
                .Take(v.Random.Number(1, 5))
                .Select(v => v.Id)
                .ToList();
            return new EditingProjectDataDto(
                v.Random.Words(),
                v.Lorem.Sentences(),
                v.Address.BuildingNumber(),
                organizerIds.First(),
                organizerIds.Skip(1).ToList(),
                date,
                new TimeOnly(7, 0).AddMinutes(v.Random.Number(0, 8) * 15),
                v.Random.Bool() ? new TimeOnly(12, 0).AddMinutes(v.Random.Number(0, 12) * 15) : null,
                new DateTime(v.Date.Between(v.Date.Soon(5), date.ToDateTime(TimeOnly.MinValue)).Ticks, DateTimeKind.Unspecified),
                v.Random.Number(1, 500)
            );
        });

    public static IReadOnlyList<ProjectOrganizer> ProjectOrganizers { get; } =
        new Faker<ProjectOrganizer>()
            .CustomInstantiator(v => new ProjectOrganizer(
                v.Random.Uuid().ToString(),
                v.Name.FirstName(),
                v.Name.LastName(),
                v.Random.String2(4).ToUpper()
            ))
        .Generate(10);

    public static IReadOnlyList<ProjectAttendee> ProjectAttendees { get; } =
        new Faker<ProjectAttendee>()
            .CustomInstantiator(v => new ProjectAttendee(
                v.Random.Uuid().ToString(),
                v.Name.FirstName(),
                v.Name.LastName(),
                $"{v.Random.Number(1, 8)}{v.Random.String2(1, "ABCD")}"
            ))
        .Generate(1000);
}
