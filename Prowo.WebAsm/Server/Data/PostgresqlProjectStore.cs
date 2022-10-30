using System.Text.Json.Serialization;
using Npgsql;

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

                async IAsyncEnumerable<DbProject> readProjects()
                {
                    using (var cmd = new NpgsqlCommand("SELECT id, title, description, location, organizer, co_organizers, date, start_time, end_time, closing_date, maxAttendees FROM project WHERE date >= @minDate", dbConnection))
                    {
                        cmd.Parameters.AddWithValue("minDate", timestamp);
                        await using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                yield return DbProject.FromReader(reader);
                            }
                        }
                    }
                }

                async IAsyncEnumerable<DbProjectRegistrationEvent> readRegistrations(Guid[] projectIds)
                {
                    using (var cmd = new NpgsqlCommand("SELECT project_id, \"user\", timestamp, action FROM registration_event WHERE project_id = ANY(@projectIds) ORDER BY project_id, timestamp", dbConnection))
                    {
                        cmd.Parameters.AddWithValue("projectIds", projectIds);
                        await using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                yield return DbProjectRegistrationEvent.FromReader(reader);
                            }
                        }
                    }
                }

                projects = await readProjects().ToList();
                registrationEvents = await readRegistrations(projects.Select(v => v.Id).ToArray()).ToList();
            }

            foreach (var project in projects)
            {
                List<ProjectAttendee> calculateActualAttendees()
                {
                    List<ProjectAttendee> result = new();
                    foreach (var entry in registrationEvents.Where(v => v.ProjectId == project.Id))
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

                yield return new Project(
                    project.Id.ToString(),
                    project.Title,
                    project.Description,
                    project.Location,
                    project.Organizer.ToProjectOrganizer(),
                    project.CoOrganizers.Select(v => v.ToProjectOrganizer()).ToList(),
                    DateOnly.FromDateTime(project.Date),
                    TimeOnly.FromTimeSpan(project.StartTime),
                    project.EndTime != null ? TimeOnly.FromTimeSpan(project.EndTime.Value) : null,
                    project.ClosingDate,
                    project.MaxAttendees,
                    calculateActualAttendees()
                );
            }
        }

        public async Task<Project> Get(string projectId)
        {
            throw new NotImplementedException();
        }

        public async Task CreateProject(Project project)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateProject(Project project)
        {
            throw new NotImplementedException();
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
            public ProjectOrganizer ToProjectOrganizer()
            {
                return new ProjectOrganizer
                (
                    Id.ToString(),
                    FirstName,
                    LastName,
                    ShortName
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
        }
    }
}
