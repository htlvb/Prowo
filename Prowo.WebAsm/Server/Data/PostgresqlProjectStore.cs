using System.Data;
using System.Linq;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;

namespace Prowo.WebAsm.Server.Data
{
    public class PostgresqlProjectStore : IDisposable
    {
        private readonly string dbConnectionString;

        public PostgresqlProjectStore(string dbConnectionString)
        {
            this.dbConnectionString = dbConnectionString;
        }

        public async IAsyncEnumerable<Project> GetAllSince(DateTime timestamp)
        {
            List<DbProject> projects;
            List<DbProjectRegistrationEvent> registrationEvents;
            await using (var dbConnection = new NpgsqlConnection(dbConnectionString))
            {
                await dbConnection.OpenAsync();
                dbConnection.TypeMapper.MapEnum<DbProjectRegistrationAction>("registration_action");

                projects = await ReadAllProjects(dbConnection, timestamp).ToList();
                registrationEvents = await ReadRegistrations(dbConnection, projects.Select(v => v.Id).ToArray()).ToList();
            }

            foreach (var project in projects)
            {
                var attendees = CalculateActualAttendees(registrationEvents.Where(v => v.ProjectId == project.Id));
                yield return project.ToDomain(attendees);
            }
        }

        public async Task<Project?> Get(string projectId)
        {
            await using var dbConnection = new NpgsqlConnection(dbConnectionString);
            await dbConnection.OpenAsync();
            dbConnection.TypeMapper.MapEnum<DbProjectRegistrationAction>("registration_action");

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
            var attendees = CalculateActualAttendees(registrationEvents);

            return dbProject.ToDomain(attendees);
        }

        public async Task CreateProject(Project project)
        {
            var dbProject = DbProject.FromDomain(project);
            await using var dbConnection = new NpgsqlConnection(dbConnectionString);
            await dbConnection.OpenAsync();
            await CreateProject(dbConnection, dbProject);
        }

        public async Task UpdateProject(Project project)
        {
            var dbProject = DbProject.FromDomain(project);
            await using var dbConnection = new NpgsqlConnection(dbConnectionString);
            await dbConnection.OpenAsync();
            await UpdateProject(dbConnection, dbProject);
        }

        public async Task<Project> AddAttendee(string projectId, ProjectAttendee attendee)
        {
            throw new NotImplementedException();
        }

        public async Task<Project> RemoveAttendee(string projectId, string userId)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        private static async IAsyncEnumerable<DbProject> ReadAllProjects(
            NpgsqlConnection dbConnection,
            DateTime minDate)
        {
            using var cmd = new NpgsqlCommand("SELECT id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees FROM project WHERE date >= @minDate", dbConnection);
            cmd.Parameters.AddWithValue("minDate", minDate);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                yield return DbProject.FromReader(reader);
            }
        }

        private async Task<DbProject?> ReadProject(NpgsqlConnection dbConnection, Guid projectGuid)
        {
            using var cmd = new NpgsqlCommand("SELECT id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees FROM project WHERE id = @projectId", dbConnection);
            cmd.Parameters.AddWithValue("projectId", projectGuid);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }
            return DbProject.FromReader(reader);
        }

        private static async Task CreateProject(NpgsqlConnection dbConnection, DbProject project)
        {
            using var cmd = new NpgsqlCommand("INSERT INTO project (id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees) VALUES (@id, @title, @description, @location, @organizer, @co_organizers, @date, @start_time, @end_time, @closing_date, @maxAttendees)", dbConnection);
            cmd.Parameters.AddWithValue("id", project.Id);
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
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task UpdateProject(NpgsqlConnection dbConnection, DbProject project)
        {
            using var cmd = new NpgsqlCommand("UPDATE project SET title=@title, description=@description, location=@location, organizer=@organizer, co_organizers=@co_organizers, date=@date, start_time=@start_time, end_time=@end_time, closing_date=@closing_date, maxAttendees=@maxAttendees WHERE id=@id", dbConnection);
            cmd.Parameters.AddWithValue("id", project.Id);
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
            await cmd.ExecuteNonQueryAsync();
        }

        private static async IAsyncEnumerable<DbProjectRegistrationEvent> ReadRegistrations(
            NpgsqlConnection dbConnection,
            params Guid[] projectIds)
        {
            using var cmd = new NpgsqlCommand("SELECT project_id, \"user\", timestamp, action FROM registration_event WHERE project_id = ANY(@projectIds) ORDER BY project_id, timestamp", dbConnection);
            cmd.Parameters.AddWithValue("projectIds", projectIds);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                yield return DbProjectRegistrationEvent.FromReader(reader);
            }
        }

        private static List<ProjectAttendee> CalculateActualAttendees(
            IEnumerable<DbProjectRegistrationEvent> registrationEvents)
        {
            List<ProjectAttendee> result = new();
            foreach (var entry in registrationEvents)
            {
                if (entry.Action == DbProjectRegistrationAction.Register && !result.Any(v => v.Id == entry.User.Id.ToString()))
                {
                    result.Add(entry.User.ToAttendee());
                }
                else if (entry.Action == DbProjectRegistrationAction.Deregister)
                {
                    result.RemoveAll(v => v.Id == entry.User.Id.ToString());
                }
            }
            return result;
        }

        private record DbProjectRegistrationUser(
            [property: JsonPropertyName("id")]Guid Id,
            [property: JsonPropertyName("first_name")]string FirstName,
            [property: JsonPropertyName("last_name")]string LastName,
            [property: JsonPropertyName("class")]string Class
        )
        {
            public ProjectAttendee ToAttendee()
            {
                return new(Id.ToString(), FirstName, LastName, Class);
            }
        }

        private enum DbProjectRegistrationAction { Register, Deregister }

        private record DbProjectRegistrationEvent(
            Guid ProjectId,
            DbProjectRegistrationUser User,
            DateTime Timestamp,
            DbProjectRegistrationAction Action 
        )
        {
            public static DbProjectRegistrationEvent FromReader(NpgsqlDataReader reader)
            {
                return new DbProjectRegistrationEvent(
                    reader.GetGuid(0),
                    reader.GetFieldValue<DbProjectRegistrationUser>(1),
                    reader.GetDateTime(2),
                    reader.GetFieldValue<DbProjectRegistrationAction>(3)
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
            public static DbProject FromReader(NpgsqlDataReader reader)
            {
                return new DbProject(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetFieldValue<DbProjectOrganizer>(4),
                    reader.GetFieldValue<DbProjectOrganizer[]>(5),
                    reader.GetDateTime(6),
                    reader.GetTimeSpan(7),
                    reader.GetFieldValue<TimeSpan?>(8),
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
