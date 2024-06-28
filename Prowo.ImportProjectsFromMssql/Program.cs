using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

string? sourceDbConnectionString = configuration.GetConnectionString("Mssql");
await using var sourceDbConnection = new SqlConnection(sourceDbConnectionString);
await sourceDbConnection.OpenAsync();

string? targetDbConnectionString = configuration.GetConnectionString("Pgsql");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(targetDbConnectionString);
dataSourceBuilder.EnableDynamicJson();
await using var dataSource = dataSourceBuilder.Build();
await using var targetDbConnection = await dataSource.OpenConnectionAsync();

await using (var cmd = new NpgsqlCommand("DELETE FROM project", targetDbConnection))
{
    await cmd.ExecuteNonQueryAsync();
}

var projects = await sourceDbConnection.QueryAsync<Project>("SELECT * FROM project");

foreach (var project in projects)
{
    Console.WriteLine($"Importing project \"{project.title}\"");
    await using (var cmd = new NpgsqlCommand("INSERT INTO project (id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees) VALUES (@id, @title, @description, @location, @organizer, @co_organizers, @date, @start_time, @end_time, @closing_date, @maxAttendees)", targetDbConnection))
    {
        cmd.Parameters.AddWithValue("id", project.id);
        cmd.Parameters.AddWithValue("title", project.title);
        cmd.Parameters.AddWithValue("description", project.description);
        cmd.Parameters.AddWithValue("location", project.location);
        cmd.Parameters.AddWithValue("organizer", NpgsqlDbType.Json, project.organizer);
        cmd.Parameters.AddWithValue("co_organizers", NpgsqlDbType.Json, project.co_organizers);
        cmd.Parameters.AddWithValue("date", project.date);
        cmd.Parameters.AddWithValue("start_time", project.start_time);
        cmd.Parameters.AddWithValue("end_time", (object?)project.end_time ?? DBNull.Value);
        cmd.Parameters.AddWithValue("closing_date", project.closing_date);
        cmd.Parameters.AddWithValue("maxAttendees", project.maxAttendees);
        await cmd.ExecuteScalarAsync();
    }
}

var registrations = await sourceDbConnection.QueryAsync<ProjectRegistrationEvent>("SELECT * FROM registration_event");
await using (var cmd = new NpgsqlCommand("INSERT INTO registration_event (project_id, \"user\", action, timestamp) SELECT UNNEST(@project_id), UNNEST(@user), UNNEST(@action), UNNEST(@timestamp)", targetDbConnection))
{
    cmd.Parameters.AddWithValue("project_id", registrations.Select(v => v.project_id).ToArray());
    cmd.Parameters.AddWithValue("user", NpgsqlDbType.Json | NpgsqlDbType.Array, registrations.Select(v => v.user).ToArray());
    cmd.Parameters.Add(new() { ParameterName = "action", Value = registrations.Select(v => v.action).ToArray(), DataTypeName = "registration_action[]" });
    cmd.Parameters.AddWithValue("timestamp", registrations.Select(v => v.timestamp).ToArray());

    await cmd.ExecuteNonQueryAsync();
}

record Project(
    Guid id,
    string title,
    string description,
    string location,
    string organizer,
    string co_organizers,
    DateTime date,
    TimeSpan start_time,
    TimeSpan? end_time,
    DateTime closing_date,
    int maxAttendees
);

record ProjectRegistrationEvent(
    Guid project_id,
    string user,
    DateTime timestamp,
    string action
);
