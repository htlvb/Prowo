using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

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
                string? description = descriptions.First(v => v.Title == title).Description;
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
    .AddJsonFile("appsettings.json")
    .Build();

string? connectionString = configuration.GetConnectionString("Mssql");
await using var dbConnection = new SqlConnection(connectionString);
await dbConnection.OpenAsync();

Console.WriteLine($"Inserting {projects.Count} projects.");
await using (var cmd = new SqlCommand("DELETE FROM project", dbConnection))
{
    await cmd.ExecuteNonQueryAsync();
}

var jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

foreach (var project in projects)
{
    await using var cmd = new SqlCommand("INSERT INTO project (id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees) VALUES (@id, @title, @description, @location, @organizer, @co_organizers, @date, @start_time, @end_time, @closing_date, @maxAttendees)", dbConnection);
    var organizer = new
    {
        id = "1d325d95-864e-4be3-ba79-60c2c92dcb61",
        first_name = "Richard",
        last_name = "Lechner",
        short_name = "LECR"
    };

    cmd.Parameters.AddWithValue("id", Guid.Parse(project.Id));
    cmd.Parameters.AddWithValue("title", project.Title);
    cmd.Parameters.AddWithValue("description", project.Description);
    cmd.Parameters.AddWithValue("location", project.Location);
    cmd.Parameters.AddWithValue("organizer", JsonSerializer.Serialize(organizer, jsonSerializerOptions));
    cmd.Parameters.AddWithValue("co_organizers", JsonSerializer.Serialize(Array.Empty<object>()));
    cmd.Parameters.AddWithValue("date", project.Date);
    cmd.Parameters.AddWithValue("start_time", project.StartTime);
    cmd.Parameters.AddWithValue("end_time", (object?)project.EndTime ?? DBNull.Value);
    cmd.Parameters.AddWithValue("closing_date", project.ClosingDate);
    cmd.Parameters.AddWithValue("maxAttendees", project.MaxAttendees);

    await cmd.ExecuteScalarAsync();
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
