using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

string? connectionString = configuration.GetConnectionString("Mssql");
await using var dbConnection = new SqlConnection(connectionString);
await dbConnection.OpenAsync();

var attendees = JsonDocument.Parse(File.ReadAllText("AttendeeCandidates.json"))
    .RootElement
    .EnumerateArray()
    .Select(v => new
    {
        UserId = v.GetProperty("ObjectId").GetString()!,
        FirstName = v.GetProperty("GivenName").GetString()!,
        LastName = v.GetProperty("Surname").GetString()!,
        Class = v.GetProperty("Department").GetString()!,
        MailAddress = v.GetProperty("UserPrincipalName").GetString()!
    })
    .ToList();
var organizers = JsonDocument.Parse(File.ReadAllText("OrganizerCandidates.json"))
    .RootElement
    .EnumerateArray()
    .Select(v =>
    {
        return new
        {
            id = v.GetProperty("ObjectId").GetString()!,
            first_name = v.GetProperty("GivenName").GetString()!,
            last_name = v.GetProperty("Surname").GetString()!,
            short_name = Regex.Replace(v.GetProperty("UserPrincipalName").GetString()!, "@.*$", ""),
        };
    })
    .ToList();
var sampleProjects = JsonDocument.Parse(File.ReadAllText("SampleProjects.json"))
    .RootElement
    .EnumerateArray()
    .Select(v =>
    {
        var date = DateTime.Today.AddDays(Random.Shared.Next(5, 7)); //DateTime.ParseExact(v.GetProperty("date").GetString()!, "d", CultureInfo.InvariantCulture);
        var startTimeString = v.GetProperty("start_time").GetString()!;
        var endTimeString = v.GetProperty("end_time").GetString()!;
        var maxAttendees = v.GetProperty("max_attendees").GetInt32();
        return new
        {
            Id = v.GetProperty("id").GetString()!,
            Title = v.GetProperty("title").GetString()!,
            Description = v.GetProperty("description").GetString()!,
            Location = v.GetProperty("location").GetString()!,
            Organizer = organizers[Random.Shared.Next(0, organizers.Count)],
            CoOrganizers = organizers
                .OrderBy(_ => Random.Shared.NextDouble())
                .Take(Random.Shared.Next(0, 10))
                .ToArray(),
            Date = date,
            StartTime = TimeSpan.ParseExact(startTimeString, "h\\:mm", CultureInfo.InvariantCulture),
            EndTime = endTimeString != null ? TimeSpan.ParseExact(endTimeString, "h\\:mm", CultureInfo.InvariantCulture) : default(TimeSpan?),
            ClosingDate = date.AddDays(Random.Shared.Next(-30, 1)).AddSeconds(-1).ToUniversalTime(),
            MaxAttendees = maxAttendees,
            RegistrationEvents = attendees
                .OrderBy(_ => Random.Shared.NextDouble())
                .Take(Random.Shared.Next(0, maxAttendees * 2))
                .Select(v => new
                {
                    userId = v.UserId,
                    firstName = v.FirstName,
                    lastName = v.LastName,
                    @class = v.Class,
                    mailAddress = v.MailAddress,
                    timestamp = DateTime.UtcNow,
                    action = "register"
                })
                .ToArray()
        };
    });

await using (var cmd = new SqlCommand("DELETE FROM project", dbConnection))
{
    await cmd.ExecuteNonQueryAsync();
}

var jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

foreach (var project in sampleProjects.Take(10))
{
    await using (var cmd = new SqlCommand("INSERT INTO project (id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees) VALUES (@id, @title, @description, @location, @organizer, @co_organizers, @date, @start_time, @end_time, @closing_date, @maxAttendees)", dbConnection))
    {
        cmd.Parameters.AddWithValue("id", Guid.Parse(project.Id));
        cmd.Parameters.AddWithValue("title", project.Title);
        cmd.Parameters.AddWithValue("description", project.Description);
        cmd.Parameters.AddWithValue("location", project.Location);
        cmd.Parameters.AddWithValue("organizer", JsonSerializer.Serialize(project.Organizer, jsonSerializerOptions));
        cmd.Parameters.AddWithValue("co_organizers", JsonSerializer.Serialize(project.CoOrganizers, jsonSerializerOptions));
        cmd.Parameters.AddWithValue("date", project.Date);
        cmd.Parameters.AddWithValue("start_time", project.StartTime);
        cmd.Parameters.AddWithValue("end_time", (object?)project.EndTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("closing_date", project.ClosingDate);
        cmd.Parameters.AddWithValue("maxAttendees", project.MaxAttendees);

        await cmd.ExecuteScalarAsync();
    }

    foreach (var registrationEvent in project.RegistrationEvents)
    {
        await using var cmd = new SqlCommand("INSERT INTO registration_event (project_id, [user], action, timestamp) VALUES(@project_id, @user, @action, @timestamp)", dbConnection);
        var user = new {
            id = registrationEvent.userId, 
            first_name = registrationEvent.firstName, 
            last_name = registrationEvent.lastName, 
            @class = registrationEvent.@class,
            mail_address = registrationEvent.mailAddress
        };

        cmd.Parameters.AddWithValue("project_id", Guid.Parse(project.Id));
        cmd.Parameters.AddWithValue("user", JsonSerializer.Serialize(user, jsonSerializerOptions));
        cmd.Parameters.AddWithValue("action", registrationEvent.action);
        cmd.Parameters.AddWithValue("timestamp", registrationEvent.timestamp);

        await cmd.ExecuteNonQueryAsync();
    }
}
