using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Server.IntegrationTests.Utils;

namespace Prowo.WebAsm.Server.IntegrationTests;

public class SingleRegistrationPerDayStrategyTests
{
    [Fact]
    public void RegistrationActionsAreCorrectlyCalculated()
    {
        var attendee = FakeData.ProjectAttendees[0];
        var projects = new[] {
            FakeData.ProjectFaker.Generate() with { Date = DateOnly.FromDateTime(DateTime.Today).AddDays(-1), AllAttendees = [attendee] },
            FakeData.ProjectFaker.Generate() with { Date = DateOnly.FromDateTime(DateTime.Today), AllAttendees = FakeData.ProjectAttendees.ToArray()[1..3] },
            FakeData.ProjectFaker.Generate() with { Date = DateOnly.FromDateTime(DateTime.Today), AllAttendees = [] },
            FakeData.ProjectFaker.Generate() with { Date = DateOnly.FromDateTime(DateTime.Today).AddDays(1), AllAttendees = FakeData.ProjectAttendees.ToArray()[..2] },
            FakeData.ProjectFaker.Generate() with { Date = DateOnly.FromDateTime(DateTime.Today).AddDays(1), AllAttendees = FakeData.ProjectAttendees.ToArray()[4..5] }
        };
        var sut = new SingleRegistrationPerDayStrategy();

        var result = sut.GetRegistrationActions(attendee, projects);
        var actual = new[]
        {
            result[projects[1]].CanRegister,
            result[projects[2]].CanRegister,
            result[projects[4]].CanRegister,
        };
        
        var expected = new [] { true, true, false };
        Assert.Equal(expected, actual);
    }
}