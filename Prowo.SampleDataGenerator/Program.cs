using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.Json;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string connectionString = configuration.GetConnectionString("CosmosDb");
using CosmosClient cosmosClient = new(connectionString);

var container = cosmosClient.GetContainer("ProjectsDB", "Project");
//await container.DeleteContainerAsync();
//container = await cosmosClient
//    .GetDatabase("ProjectsDB")
//    .CreateContainerAsync("Project", "/id");

var sampleProjects = JsonDocument.Parse(File.ReadAllText("SampleProjects.json"));
var sampleUsers = JsonDocument.Parse(File.ReadAllText("SampleUsers.json"));
foreach (var project in sampleProjects.RootElement.EnumerateArray().Take(10))
{
    var maxAttendees = project.GetProperty("max_attendees").GetInt32();
    var date = DateOnly.ParseExact(project.GetProperty("date").GetString()!, "d", CultureInfo.InvariantCulture);
    var startTimeString = project.GetProperty("start_time").GetString()!;
    var startTime = TimeOnly.ParseExact(startTimeString, "H:mm", CultureInfo.InvariantCulture);
    var endTimeString = project.GetProperty("end_time").GetString();
    var endTime = endTimeString != null ? TimeOnly.ParseExact(endTimeString, "H:mm", CultureInfo.InvariantCulture) : default(TimeOnly?);

    await container.CreateItemAsync(new
    {
        id = Guid.NewGuid().ToString(),
        title = project.GetProperty("title").GetString(),
        description = project.GetProperty("description").GetString(),
        location = project.GetProperty("location").GetString(),
        organizerId = Guid.NewGuid().ToString(),
        coOrganizerIds = Enumerable
            .Range(0, Random.Shared.Next(0, 10))
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray(),
        date = date.ToDateTime(TimeOnly.MinValue),
        startTime = startTime.ToTimeSpan(),
        endTime = endTime?.ToTimeSpan(),
        maxAttendees = maxAttendees,
        registrationEvents = sampleUsers.RootElement.EnumerateArray()
            .OrderBy(_ => Random.Shared.NextDouble())
            .Take(Random.Shared.Next(0, maxAttendees + 1))
            .Select(v => new
            {
                userId = Guid.NewGuid().ToString(),
                firstName = v.GetProperty("first_name").GetString(),
                lastName = v.GetProperty("last_name").GetString(),
                @class = v.GetProperty("class").GetString(),
                timestamp = DateTime.UtcNow,
                action = "register"
            })
            .ToArray()
    });
}
