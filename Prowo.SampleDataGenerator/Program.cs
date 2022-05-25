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

var attendees = JsonDocument.Parse(File.ReadAllText("AttendeeCandidates.json"))
    .RootElement
    .EnumerateArray()
    .Select(v => new
    {
        UserId = v.GetProperty("ObjectId").GetString()!,
        FirstName = v.GetProperty("GivenName").GetString()!,
        LastName = v.GetProperty("Surname").GetString()!,
        Class = v.GetProperty("Department").GetString()!
    })
    .ToList();
var organizers = JsonDocument.Parse(File.ReadAllText("OrganizerCandidates.json"))
    .RootElement
    .EnumerateArray()
    .Select(v => v.GetString())
    .ToList();
var sampleProjects = JsonDocument.Parse(File.ReadAllText("SampleProjects.json"))
    .RootElement
    .EnumerateArray()
    .Select(v =>
    {
        var date = DateTime.ParseExact(v.GetProperty("date").GetString()!, "d", CultureInfo.InvariantCulture);
        var startTimeString = v.GetProperty("start_time").GetString()!;
        var endTimeString = v.GetProperty("end_time").GetString()!;
        var maxAttendees = v.GetProperty("max_attendees").GetInt32();
        return new
        {
            Id = v.GetProperty("id").GetString()!,
            Title = v.GetProperty("title").GetString()!,
            Description = v.GetProperty("description").GetString()!,
            Location = v.GetProperty("location").GetString()!,
            Date = date,
            StartTime = TimeSpan.ParseExact(startTimeString, "h\\:mm", CultureInfo.InvariantCulture),
            EndTime = endTimeString != null ? TimeSpan.ParseExact(endTimeString, "h\\:mm", CultureInfo.InvariantCulture) : default(TimeSpan?),
            ClosingDate = date.AddDays(Random.Shared.Next(-30, 1)).ToUniversalTime(),
            MaxAttendees = maxAttendees
        };
    });
foreach (var project in sampleProjects.Take(10))
{
    await container.CreateItemAsync(new
    {
        id = project.Id,
        title = project.Title,
        description = project.Description,
        location = project.Location,
        organizerId = organizers[Random.Shared.Next(0, organizers.Count)],
        coOrganizerIds = organizers
            .OrderBy(_ => Random.Shared.NextDouble())
            .Take(Random.Shared.Next(0, 10))
            .ToArray(),
        date = project.Date,
        startTime = project.StartTime,
        endTime = project.EndTime,
        closingDate = project.ClosingDate,
        maxAttendees = project.MaxAttendees,
        registrationEvents = attendees
            .OrderBy(_ => Random.Shared.NextDouble())
            .Take(Random.Shared.Next(0, project.MaxAttendees * 2))
            .Select(v => new
            {
                userId = v.UserId,
                firstName = v.FirstName,
                lastName = v.LastName,
                @class = v.Class,
                timestamp = DateTime.UtcNow,
                action = "register"
            })
            .ToArray()
    });
}
