using ClosedXML.Excel;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

var descriptions = File.ReadAllText("Gesundheitstag_Beschreibungen.txt")
    .Split(new[] { "\r\n\r\n" }, StringSplitOptions.None)
    .Select(v =>
    {
        var parts = v.Split("\r\n", 2);
        return new
        {
            Title = parts[0],
            Description = parts.Length > 1 ? parts[1].Replace("\r\n", "\n") : null
        };
    })
    .ToList();

List<Project> projects = new();
using (var workbook = new XLWorkbook("Gesundheitstag_2022_10_17.xlsx"))
{
    var worksheet = workbook.Worksheets.Worksheet("Timetable");
    for (int column = 3; column <= 31; column += 2)
    {
        var maxAttendeesCell = worksheet.Cell(2, column);
        var startTimeCell = GetTimeCell(maxAttendeesCell);
        while (!startTimeCell.IsEmpty())
        {
            if (!maxAttendeesCell.IsEmpty())
            {
                var titleCell = maxAttendeesCell.CellLeft();
                titleCell = titleCell.MergedRange()?.FirstCell() ?? titleCell;
                var text = titleCell.GetRichText()
                    .Select(v => v.Text
                        .Trim(' ', ' ', '\t', '•', '·', '–', '-', '⁞')
                        .Replace("„", "")
                        .Replace("“", "")
                        .Replace("–", "-")
                        .Replace("  ", " ")
                    )
                    .Where(v => v != "").ToList();
                var title = text[0];
                string description = descriptions.First(v => v.Title == title).Description;
                var fullDescription = string.Join(
                    "\n\n",
                    new[] {
                        description,
                        text.Count > 1 ? text[1] : null
                    }
                    .Where(v => v != null)
                );
                var (startTime, _) = GetTimes(startTimeCell);
                var lastCell = maxAttendeesCell.MergedRange()?.LastCell() ?? maxAttendeesCell;
                var endTimeCell = GetTimeCell(lastCell.CellBelow()).CellAbove();
                var (_, endTime) = GetTimes(endTimeCell);
                var room = GetRoomCell(titleCell).GetString();
                var maxAttendees = maxAttendeesCell.GetValue<int>();
                Console.WriteLine($"{startTime} - {endTime} ({room}) ({maxAttendees}): {string.Join(" - ", text.Select(v => $"<{v}>"))}");
                projects.Add(new Project
                (
                    Guid.NewGuid().ToString(),
                    title,
                    fullDescription,
                    room,
                    new DateOnly(2022, 10, 25),
                    startTime,
                    endTime,
                    new DateTime(2022, 10, 24, 12, 00, 00).ToUniversalTime(),
                    maxAttendees
                ));
            }
            maxAttendeesCell = maxAttendeesCell.CellBelow();
            startTimeCell = GetTimeCell(maxAttendeesCell);
        }
    }
}

IXLCell GetTimeCell(IXLCell cell)
{
    return cell.CellLeft(cell.Address.ColumnNumber - 1);
}

(TimeOnly, TimeOnly) GetTimes(IXLCell timeCell)
{
    var parts = timeCell.GetString().Split("-").Select(v => v.Trim()).ToList();
    var startTime = TimeOnly.Parse(parts[0]);
    var endTime = TimeOnly.Parse(parts[1]);
    return (startTime, endTime);
}

IXLCell GetRoomCell(IXLCell cell)
{
    return cell.CellAbove(cell.Address.RowNumber - 1);
}

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string connectionString = configuration.GetConnectionString("CosmosDb");
using CosmosClient cosmosClient = new(connectionString);

var container = cosmosClient.GetContainer("ProjectsDB", "Project");
await container.DeleteContainerAsync();
container = await cosmosClient
   .GetDatabase("ProjectsDB")
   .CreateContainerAsync("Project", "/id");

Console.WriteLine($"Inserting {projects.Count} projects.");
foreach (var project in projects)
{
    await container.CreateItemAsync(new
    {
        id = project.Id,
        title = project.Title,
        description = project.Description,
        location = project.Location,
        organizer = new {
            id = "1d325d95-864e-4be3-ba79-60c2c92dcb61",
            firstName = "Richard",
            lastName = "Lechner",
            shortName = "LECR"
        },
        coOrganizers = Array.Empty<object>(),
        date = project.Date.ToDateTime(TimeOnly.MinValue),
        startTime = project.StartTime.ToTimeSpan(),
        endTime = project.EndTime.ToTimeSpan(),
        closingDate = project.ClosingDate,
        maxAttendees = project.MaxAttendees,
        registrationEvents = Array.Empty<object>()
    });
}

public record Project(
    string Id,
    string Title,
    string Description,
    string Location,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateTime ClosingDate,
    int MaxAttendees
);
