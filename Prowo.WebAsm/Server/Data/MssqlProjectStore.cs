using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prowo.WebAsm.Server.Data
{
    public class MssqlProjectStore : IProjectStore, IDisposable
    {
        private readonly string dbConnectionString;
        private static readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public MssqlProjectStore(string dbConnectionString)
        {
            this.dbConnectionString = dbConnectionString;
        }

        public async IAsyncEnumerable<Project> GetAllSince(DateTime timestamp)
        {
            List<DbProject> projects;
            List<DbProjectRegistrationEvent> registrationEvents;
            await using (var dbConnection = await Connect())
            {
                projects = await ReadAllProjects(dbConnection, timestamp).ToList();
                registrationEvents = await ReadRegistrations(dbConnection, projects.Select(v => v.Id).ToArray()).ToList();
            }

            foreach (var project in projects)
            {
                var attendees = CalculateActualAttendees(registrationEvents.Where(v => v.ProjectId == project.Id), project.MaxAttendees);
                yield return project.ToDomain(attendees);
            }
        }

        public async Task<Project?> Get(string projectId)
        {
            await using var dbConnection = await Connect();

            if (!Guid.TryParse(projectId, out var projectGuid))
            {
                return null;
            }
            var dbProject = await ReadProject(dbConnection, projectGuid);
            if (dbProject == null)
            {
                return null;
            }
            var registrationEvents = await ReadRegistrations(dbConnection, dbProject.Id).ToList();
            var attendees = CalculateActualAttendees(registrationEvents, dbProject.MaxAttendees);

            return dbProject.ToDomain(attendees);
        }

        public async Task Delete(string projectId)
        {
            if (!Guid.TryParse(projectId, out var projectGuid))
            {
                return;
            }
            await using var dbConnection = await Connect();
            using var cmd = new SqlCommand("DELETE FROM project WHERE id = @id", dbConnection);
            cmd.Parameters.AddWithValue("id", projectGuid);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task CreateProject(Project project)
        {
            var dbProject = DbProject.FromDomain(project);
            await using var dbConnection = await Connect();
            await CreateProject(dbConnection, dbProject);
        }

        public async Task UpdateProject(Project project)
        {
            var dbProject = DbProject.FromDomain(project);
            await using var dbConnection = await Connect();
            await UpdateProject(dbConnection, dbProject);
        }

        public async Task<Project> AddAttendee(string projectId, ProjectAttendee attendee)
        {
            var dbRegistrationEvent = new DbProjectRegistrationEvent(
                Guid.Parse(projectId),
                DbProjectRegistrationUser.FromAttendee(attendee),
                DateTime.UtcNow,
                "register"
            );
            await using var dbConnection = await Connect();
            await AddRegistrationEvent(dbConnection, dbRegistrationEvent);
            return (await Get(projectId))!;
        }

        public async Task<Project> RemoveAttendee(string projectId, string userId)
        {
            var dbRegistrationEvent = new DbProjectRegistrationEvent(
                Guid.Parse(projectId),
                new DbProjectRegistrationUser(Guid.Parse(userId), null, null, null, null),
                DateTime.UtcNow,
                "deregister"
            );
            await using var dbConnection = await Connect();
            await AddRegistrationEvent(dbConnection, dbRegistrationEvent);
            return (await Get(projectId))!;
        }

        public void Dispose()
        {
        }

        private static async IAsyncEnumerable<DbProject> ReadAllProjects(
            SqlConnection dbConnection,
            DateTime minDate)
        {
            using var cmd = new SqlCommand("SELECT id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees FROM project WHERE date >= @minDate", dbConnection);
            cmd.Parameters.AddWithValue("minDate", minDate);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                yield return DbProject.FromReader(reader);
            }
        }

        private static async Task<DbProject?> ReadProject(SqlConnection dbConnection, Guid projectGuid)
        {
            using var cmd = new SqlCommand("SELECT id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees FROM project WHERE id = @projectId", dbConnection);
            cmd.Parameters.AddWithValue("projectId", projectGuid);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }
            return DbProject.FromReader(reader);
        }

        private async Task<SqlConnection> Connect()
        {
            var dbConnection = new SqlConnection(dbConnectionString)
            {
                RetryLogicProvider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(new SqlRetryLogicOption { NumberOfTries = 5 })
            };
            await dbConnection.OpenAsync();
            return dbConnection;
        }

        private static async Task CreateProject(SqlConnection dbConnection, DbProject project)
        {
            using var cmd = new SqlCommand("INSERT INTO project (id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees) VALUES (@id, @title, @description, @location, @organizer, @co_organizers, @date, @start_time, @end_time, @closing_date, @maxAttendees)", dbConnection);
            cmd.Parameters.AddWithValue("id", project.Id);
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
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task UpdateProject(SqlConnection dbConnection, DbProject project)
        {
            using var cmd = new SqlCommand("UPDATE project SET title=@title, description=@description, location=@location, organizer=@organizer, co_organizers=@co_organizers, date=@date, start_time=@start_time, end_time=@end_time, closing_date=@closing_date, maxAttendees=@maxAttendees WHERE id=@id", dbConnection);
            cmd.Parameters.AddWithValue("id", project.Id);
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
            await cmd.ExecuteNonQueryAsync();
        }

        private static async IAsyncEnumerable<DbProjectRegistrationEvent> ReadRegistrations(
            SqlConnection dbConnection,
            params Guid[] projectIds)
        {
            if (projectIds.Length == 0)
            {
                yield break;
            }
            var projectIdParameterNames = Enumerable
                .Range(1, projectIds.Length)
                .Select(i => $"@projectId{i}");
            var projectIdParameters = Enumerable
                .Range(1, projectIds.Length)
                .Select(i => new SqlParameter($"projectId{i}", projectIds[i - 1]))
                .ToArray();
            using var cmd = new SqlCommand($"SELECT project_id, [user], timestamp, action FROM registration_event WHERE project_id IN({string.Join(", ", projectIdParameterNames)}) ORDER BY project_id, timestamp", dbConnection);
            cmd.Parameters.AddRange(projectIdParameters);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                yield return DbProjectRegistrationEvent.FromReader(reader);
            }
        }

        private static async Task AddRegistrationEvent(
            SqlConnection dbConnection,
            DbProjectRegistrationEvent registrationEvent)
        {
            using var cmd = new SqlCommand("INSERT INTO registration_event (project_id, [user], timestamp, action) VALUES (@project_id, @user, @timestamp, @action)", dbConnection);
            cmd.Parameters.AddWithValue("project_id", registrationEvent.ProjectId);
            cmd.Parameters.AddWithValue("user", JsonSerializer.Serialize(registrationEvent.User, jsonSerializerOptions));
            cmd.Parameters.AddWithValue("timestamp", registrationEvent.Timestamp);
            cmd.Parameters.AddWithValue("action", registrationEvent.Action);
            await cmd.ExecuteNonQueryAsync();
        }

        private static List<ProjectAttendee> CalculateActualAttendees(
            IEnumerable<DbProjectRegistrationEvent> registrationEvents,
            int maxAttendees)
        {
            List<ProjectAttendee> result = new();
            foreach (var entry in registrationEvents)
            {
                if (entry.Action == "register" && !result.Any(v => v.Id == entry.User.Id.ToString()))
                {
                    if (result.Count < maxAttendees) // Ignore registrations while attendee list was full
                    {
                        result.Add(entry.User.ToAttendee());
                    }
                }
                else if (entry.Action == "deregister")
                {
                    result.RemoveAll(v => v.Id == entry.User.Id.ToString());
                }
            }
            return result;
        }

        private record DbProjectRegistrationUser(
            [property: JsonPropertyName("id")]Guid Id,
            [property: JsonPropertyName("first_name")]string? FirstName,
            [property: JsonPropertyName("last_name")]string? LastName,
            [property: JsonPropertyName("class")]string? Class,
            [property: JsonPropertyName("mail_address")]string? MailAddress
        )
        {
            public ProjectAttendee ToAttendee()
            {
                if (FirstName == null || LastName == null || Class == null || MailAddress == null)
                {
                    throw new InvalidOperationException($"Can't convert {nameof(DbProjectRegistrationUser)} to {nameof(ProjectAttendee)} because it looks like a deregistration.");
                }
                return new(Id.ToString(), FirstName, LastName, Class, MailAddress);
            }

            public static DbProjectRegistrationUser FromAttendee(ProjectAttendee attendee)
            {
                return new(Guid.Parse(attendee.Id), attendee.FirstName, attendee.LastName, attendee.Class, attendee.MailAddress);
            }
        }

        private record DbProjectRegistrationEvent(
            Guid ProjectId,
            DbProjectRegistrationUser User,
            DateTime Timestamp,
            string Action 
        )
        {
            public static DbProjectRegistrationEvent FromReader(SqlDataReader reader)
            {
                return new DbProjectRegistrationEvent(
                    reader.GetGuid(0),
                    JsonSerializer.Deserialize<DbProjectRegistrationUser>(reader.GetString(1), jsonSerializerOptions)!,
                    reader.GetDateTime(2),
                    reader.GetString(3)
                );
            }
        }

        private record DbProjectOrganizer(
            [property: JsonPropertyName("id")] Guid Id,
            [property: JsonPropertyName("first_name")] string FirstName,
            [property: JsonPropertyName("last_name")] string LastName,
            [property: JsonPropertyName("short_name")] string ShortName
        )
        {
            public ProjectOrganizer ToDomain()
            {
                return new ProjectOrganizer
                (
                    Id.ToString(),
                    FirstName,
                    LastName,
                    ShortName
                );
            }

            public static DbProjectOrganizer FromDomain(ProjectOrganizer organizer)
            {
                return new DbProjectOrganizer
                (
                    Guid.Parse(organizer.Id),
                    organizer.FirstName,
                    organizer.LastName,
                    organizer.ShortName
                );
            }
        }

        private record DbProject(
            Guid Id,
            string Title,
            string Description,
            string Location,
            DbProjectOrganizer Organizer,
            IReadOnlyCollection<DbProjectOrganizer> CoOrganizers,
            DateTime Date,
            TimeSpan StartTime,
            TimeSpan? EndTime,
            DateTime ClosingDate,
            int MaxAttendees
        )
        {
            public static DbProject FromReader(SqlDataReader reader)
            {
                return new DbProject(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    JsonSerializer.Deserialize<DbProjectOrganizer>(reader.GetString(4), jsonSerializerOptions)!,
                    JsonSerializer.Deserialize<DbProjectOrganizer[]>(reader.GetString(5), jsonSerializerOptions)!,
                    reader.GetDateTime(6),
                    reader.GetTimeSpan(7),
                    reader.IsDBNull(8) ? null : reader.GetTimeSpan(8),
                    reader.GetDateTime(9),
                    reader.GetInt32(10)
                );
            }

            public static DbProject FromDomain(Project project)
            {
                return new DbProject(
                    Guid.Parse(project.Id),
                    project.Title,
                    project.Description,
                    project.Location,
                    DbProjectOrganizer.FromDomain(project.Organizer),
                    project.CoOrganizers.Select(DbProjectOrganizer.FromDomain).ToList(),
                    project.Date.ToDateTime(TimeOnly.MinValue),
                    project.StartTime.ToTimeSpan(),
                    project.EndTime?.ToTimeSpan(),
                    project.ClosingDate,
                    project.MaxAttendees
                );
            }

            public Project ToDomain(IReadOnlyList<ProjectAttendee> attendees)
            {
                return new Project(
                    Id.ToString(),
                    Title,
                    Description,
                    Location,
                    Organizer.ToDomain(),
                    CoOrganizers.Select(v => v.ToDomain()).ToList(),
                    DateOnly.FromDateTime(Date),
                    TimeOnly.FromTimeSpan(StartTime),
                    EndTime != null ? TimeOnly.FromTimeSpan(EndTime.Value) : null,
                    ClosingDate,
                    MaxAttendees,
                    attendees
                );
            }
        }
    }
}
