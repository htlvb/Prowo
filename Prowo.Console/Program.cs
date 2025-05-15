using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

string? connectionString = configuration.GetConnectionString("Pgsql");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
await using var dataSource = dataSourceBuilder.Build();
await using var dbConnection = await dataSource.OpenConnectionAsync();

var graphServiceClient = new GraphServiceClient(new DefaultAzureCredential());
UserCollectionResponse? usersResponse = await graphServiceClient.Users.GetAsync(v =>
    {
        v.QueryParameters.Filter = "surname in ('Wagner', 'Lohninger') and department eq '4BHME'";
        v.QueryParameters.Select = ["id", "givenName", "surName", "department", "userPrincipalName"];
    });
List<User> users = usersResponse?.Value ?? [];
var dbUsers = users
    .Select(v => new { id = v.Id, first_name = v.GivenName, last_name = v.Surname, @class = v.Department, mail_address = v.UserPrincipalName })
    .ToArray();
if (dbUsers.Any(v => v.id == null || v.first_name == null || v.last_name == null || v.@class == null || v.mail_address == null))
{
    Console.WriteLine("Some user properties are null.");
    Console.WriteLine(JsonSerializer.Serialize(dbUsers));
    return;
}
Console.Write($"Found {dbUsers.Length} {(dbUsers.Length == 1 ? "user" : "users")}. Continue? [Y/n] ");
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
    cmd.Parameters.AddWithValue("project_id", Enumerable.Repeat(projectId, users.Count).ToArray());
    cmd.Parameters.AddWithValue("user", NpgsqlDbType.Array | NpgsqlDbType.Json, dbUsers);
    cmd.Parameters.Add(new() { ParameterName = "action", Value = Enumerable.Repeat("register", users.Count).ToArray(), DataTypeName = "registration_action[]" });
    cmd.Parameters.AddWithValue("timestamp", Enumerable.Repeat(DateTime.UtcNow, users.Count).ToArray());

    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine("Finished.");
