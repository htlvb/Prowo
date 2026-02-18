using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

string? connectionString = configuration.GetConnectionString("Pgsql");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
await using var dataSource = dataSourceBuilder.Build();
await using var dbConnection = await dataSource.OpenConnectionAsync();

var users = new[] {
    new { id = "", first_name = "", last_name = "", @class = "", mail_address = "" }
};
Console.Write($"Continue to insert {users.Length} {(users.Length == 1 ? "user" : "users")}? [Y/n] ");
if (string.Equals(Console.ReadLine(), "n", StringComparison.InvariantCultureIgnoreCase))
{
    Console.WriteLine("Cancelled.");
    return;
}

Guid? projectId;
await using (var cmd = new NpgsqlCommand("SELECT id FROM project WHERE title='RoboLab (4AHME, 4BHME)'", dbConnection))
{
    projectId = (Guid?)await cmd.ExecuteScalarAsync();
}
if (projectId == null)
{
    Console.WriteLine("Error: Project not found.");
    return;
}
// await using (var cmd = new NpgsqlCommand("DELETE FROM registration_event WHERE project_id=@project_id", dbConnection))
// {
//     cmd.Parameters.AddWithValue("project_id", projectId);
//     await cmd.ExecuteNonQueryAsync();
//     return;
// }
await using (var cmd = new NpgsqlCommand("INSERT INTO registration_event (project_id, \"user\", action, timestamp) SELECT UNNEST(@project_id), UNNEST(@user), UNNEST(@action), UNNEST(@timestamp)", dbConnection))
{
    cmd.Parameters.AddWithValue("project_id", Enumerable.Repeat(projectId, users.Length).ToArray());
    cmd.Parameters.AddWithValue("user", NpgsqlDbType.Array | NpgsqlDbType.Json, users);
    cmd.Parameters.Add(new() { ParameterName = "action", Value = Enumerable.Repeat("register", users.Length).ToArray(), DataTypeName = "registration_action[]" });
    cmd.Parameters.AddWithValue("timestamp", Enumerable.Repeat(DateTime.UtcNow, users.Length).ToArray());

    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine("Finished.");
