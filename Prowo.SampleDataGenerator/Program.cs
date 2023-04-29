using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

NpgsqlConnection.GlobalTypeMapper.UseJsonNet();
NpgsqlConnection.GlobalTypeMapper.MapEnum<RegistrationAction>("registration_action");

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

string? connectionString = configuration.GetConnectionString("PostgresqlDb");
await using var dbConnection = new NpgsqlConnection(connectionString);
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
            ClosingDate = date.AddDays(Random.Shared.Next(-30, 1)).ToUniversalTime(),
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
                    action = RegistrationAction.Register
                })
                .ToArray()
        };
    });

await using (var cmd = new NpgsqlCommand("DELETE FROM project", dbConnection))
{
    await cmd.ExecuteNonQueryAsync();
}

foreach (var project in sampleProjects.Take(10))
{
    await using (var cmd = new NpgsqlCommand("INSERT INTO project (id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees) VALUES (@id, @title, @description, @location, @organizer, @co_organizers, @date, @start_time, @end_time, @closing_date, @maxAttendees)", dbConnection))
    {
        cmd.Parameters.AddWithValue("id", Guid.Parse(project.Id));
        cmd.Parameters.AddWithValue("title", project.Title);
        cmd.Parameters.AddWithValue("description", project.Description);
        cmd.Parameters.AddWithValue("location", project.Location);
        cmd.Parameters.AddWithValue("organizer", NpgsqlDbType.Json, project.Organizer);
        cmd.Parameters.AddWithValue("co_organizers", NpgsqlDbType.Json, project.CoOrganizers);
        cmd.Parameters.AddWithValue("date", project.Date);
        cmd.Parameters.AddWithValue("start_time", project.StartTime);
        cmd.Parameters.AddWithValue("end_time", (object?)project.EndTime ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("closing_date", project.ClosingDate);
        cmd.Parameters.AddWithValue("maxAttendees", project.MaxAttendees);

        await cmd.ExecuteScalarAsync();
    }

    await using (var cmd = new NpgsqlCommand("INSERT INTO registration_event (project_id, \"user\", action, timestamp) SELECT UNNEST(@project_id), UNNEST(@user), UNNEST(@action), UNNEST(@timestamp)", dbConnection))
    {
        cmd.Parameters.AddWithValue("project_id", Enumerable.Repeat(Guid.Parse(project.Id), project.RegistrationEvents.Length).ToArray());
        cmd.Parameters.AddWithValue("user", NpgsqlDbType.Array | NpgsqlDbType.Json, project.RegistrationEvents.Select(v => new { id = v.userId, first_name = v.firstName, last_name = v.lastName, @class = v.@class, mail_address = v.mailAddress}).ToArray());
        cmd.Parameters.AddWithValue("action", project.RegistrationEvents.Select(v => v.action).ToArray());
        cmd.Parameters.AddWithValue("timestamp", project.RegistrationEvents.Select(v => v.timestamp).ToArray());

        await cmd.ExecuteNonQueryAsync();
    }
}

enum RegistrationAction { Register, Deregister }
