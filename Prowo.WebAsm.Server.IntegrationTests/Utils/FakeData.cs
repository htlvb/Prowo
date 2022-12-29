using Bogus;
using Prowo.WebAsm.Server;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Shared;

namespace Prowo.WebAsm.Server.IntegrationTests.Utils;

public static class FakeData
{
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
                v.Lorem.Text(),
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
}
