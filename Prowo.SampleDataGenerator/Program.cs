using Keycloak.AdminApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Npgsql;
using NpgsqlTypes;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
var attendeeCandidatesFilePath = Path.Combine(dir, "AttendeeCandidates.json");
var organizerCandidatesFilePath = Path.Combine(dir, "OrganizerCandidates.json");

if (!File.Exists(attendeeCandidatesFilePath) ||
    !File.Exists(organizerCandidatesFilePath) ||
    args is [ "--load-data", .. ])
{
    var accessTokenProvider = new KeycloakAccessTokenProvider("id.htlvb.at");
    var authProvider = new BaseBearerTokenAuthenticationProvider(accessTokenProvider);
    var adapter = new HttpClientRequestAdapter(authProvider)
    {
        BaseUrl = "https://id.htlvb.at"
    };
    var keycloakAdminApiClient = new KeycloakAdminApiClient(adapter);
    var teachers = await keycloakAdminApiClient.Admin
        .Realms["htlvb"]
        .Groups["6c766d94-3dec-4cf5-94f7-b327b40c56b2"]
        .Members
        .GetAsync(v => v.QueryParameters.Max = -1);
    var teacherData = teachers?
        .Select(v => new { Id = v.Id, FirstName = v.FirstName, LastName = v.LastName, ShortName = v.Username?.ToUpper() });
    File.WriteAllText(organizerCandidatesFilePath, JsonSerializer.Serialize(teacherData, new JsonSerializerOptions { WriteIndented = true }));

    var studentGroups = await keycloakAdminApiClient.Admin
        .Realms["htlvb"]
        .Groups["3d6bfb52-6e94-4439-bff3-0813a500963a"]
        .Children
        .GetAsync(v => v.QueryParameters.Max = -1);
    var studentDataTasks = studentGroups?
        .Select(async studentGroup =>
        {
            var students = await keycloakAdminApiClient.Admin
                .Realms["htlvb"]
                .Groups[studentGroup.Id]
                .Members
                .GetAsync(v => v.QueryParameters.Max = -1);
            return students?
                .Select(v => new { Id = v.Id, FirstName = v.FirstName, LastName = v.LastName, Class = studentGroup.Name, MailAddress = v.Email }) ?? [];
        }) ?? [];
    var studentData = (await Task.WhenAll(studentDataTasks))
        .SelectMany(v => v);
    File.WriteAllText(attendeeCandidatesFilePath, JsonSerializer.Serialize(studentData, new JsonSerializerOptions { WriteIndented = true }));
}

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

string? connectionString = configuration.GetConnectionString("Pgsql");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
await using var dataSource = dataSourceBuilder.Build();
await using var dbConnection = await dataSource.OpenConnectionAsync();

var attendees = JsonDocument.Parse(File.ReadAllText(attendeeCandidatesFilePath))
    .RootElement
    .EnumerateArray()
    .Select(v => new
    {
        UserId = v.GetProperty("Id").GetString()!,
        FirstName = v.GetProperty("FirstName").GetString()!,
        LastName = v.GetProperty("LastName").GetString()!,
        Class = v.GetProperty("Class").GetString()!,
        MailAddress = v.GetProperty("MailAddress").GetString()!
    })
    .ToList();
var organizers = JsonDocument.Parse(File.ReadAllText(organizerCandidatesFilePath))
    .RootElement
    .EnumerateArray()
    .Select(v =>
    {
        return new
        {
            id = v.GetProperty("Id").GetString()!,
            first_name = v.GetProperty("FirstName").GetString()!,
            last_name = v.GetProperty("LastName").GetString()!,
            short_name = Regex.Replace(v.GetProperty("ShortName").GetString()!, "@.*$", ""),
        };
    })
    .ToList();
var sampleProjects = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "SampleProjects.json")))
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
            ClosingDate = date.AddDays(Random.Shared.Next(-14, 1)).AddSeconds(-1).ToUniversalTime(),
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

await using (var cmd = new NpgsqlCommand("DELETE FROM project", dbConnection))
{
    await cmd.ExecuteNonQueryAsync();
}

foreach (var project in sampleProjects.Take(50))
{
    Console.WriteLine($"Creating project \"{project.Title}\"");
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
        cmd.Parameters.AddWithValue("end_time", (object?)project.EndTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("closing_date", project.ClosingDate);
        cmd.Parameters.AddWithValue("maxAttendees", project.MaxAttendees);

        await cmd.ExecuteScalarAsync();
    }

    await using (var cmd = new NpgsqlCommand("INSERT INTO registration_event (project_id, \"user\", action, timestamp) SELECT UNNEST(@project_id), UNNEST(@user), UNNEST(@action), UNNEST(@timestamp)", dbConnection))
    {
        cmd.Parameters.AddWithValue("project_id", Enumerable.Repeat(Guid.Parse(project.Id), project.RegistrationEvents.Length).ToArray());
        var users = project.RegistrationEvents
            .Select(v => new { id = v.userId, first_name = v.firstName, last_name = v.lastName, @class = v.@class, mail_address = v.mailAddress })
            .ToArray();
        cmd.Parameters.AddWithValue("user", NpgsqlDbType.Array | NpgsqlDbType.Json, users);
        cmd.Parameters.Add(new() { ParameterName = "action", Value = project.RegistrationEvents.Select(v => v.action).ToArray(), DataTypeName = "registration_action[]" });
        cmd.Parameters.AddWithValue("timestamp", project.RegistrationEvents.Select(v => v.timestamp).ToArray());

        await cmd.ExecuteNonQueryAsync();
    }
}
