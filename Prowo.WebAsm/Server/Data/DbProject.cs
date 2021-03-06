using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Prowo.WebAsm.Server.Data
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class DbProject
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public DbProjectOrganizer Organizer { get; set; }
        public DbProjectOrganizer[] CoOrganizers { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public DateTime ClosingDate { get; set; }
        public int MaxAttendees { get; set; }
        public RegistrationEvent[] RegistrationEvents { get; set; }

        public List<ProjectAttendee> CalculateActualAttendees()
        {
            List<ProjectAttendee> result = new();
            foreach (var entry in RegistrationEvents)
            {
                if (entry.Action == RegistrationAction.Register && !result.Any(v => v.Id == entry.UserId))
                {
                    result.Add(entry.ToAttendee());
                }
                else if (entry.Action == RegistrationAction.Deregister)
                {
                    result.RemoveAll(v => v.Id == entry.UserId);
                }
            }
            return result;
        }

        public Project ToProject()
        {
            return new(
                Id,
                Title,
                Description,
                Location,
                Organizer.ToProjectOrganizer(),
                CoOrganizers.Select(v => v.ToProjectOrganizer()).ToArray(),
                DateOnly.FromDateTime(Date),
                TimeOnly.FromTimeSpan(StartTime),
                EndTime != null ? TimeOnly.FromTimeSpan(EndTime.Value) : null,
                ClosingDate,
                MaxAttendees,
                CalculateActualAttendees()
            );
        }

        // TODO separate attendees from project data to ensure we never overwrite attendees
        public static DbProject FromProject(Project project)
        {
            return new DbProject
            {
                Id = project.Id,
                Title = project.Title,
                Description = project.Description,
                Location = project.Location,
                Organizer = DbProjectOrganizer.FromProjectOrganizer(project.Organizer),
                CoOrganizers = project.CoOrganizers.Select(DbProjectOrganizer.FromProjectOrganizer).ToArray(),
                Date = project.Date.ToDateTime(TimeOnly.MinValue),
                StartTime = project.StartTime.ToTimeSpan(),
                EndTime = project.EndTime.HasValue ? project.EndTime.Value.ToTimeSpan() : null,
                ClosingDate = project.ClosingDate,
                MaxAttendees = project.MaxAttendees,
                RegistrationEvents = Array.Empty<RegistrationEvent>()
            };
        }

        [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
        public class DbProjectOrganizer
        {
            public string Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string ShortName { get; set; }

            public static DbProjectOrganizer FromProjectOrganizer(ProjectOrganizer projectOrganizer)
            {
                return new DbProjectOrganizer
                {
                    Id = projectOrganizer.Id,
                    FirstName = projectOrganizer.FirstName,
                    LastName = projectOrganizer.LastName,
                    ShortName = projectOrganizer.ShortName
                };
            }

            public ProjectOrganizer ToProjectOrganizer()
            {
                return new ProjectOrganizer
                (
                    Id,
                    FirstName,
                    LastName,
                    ShortName
                );
            }
        }

        [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
        public class RegistrationEvent
        {
            public string UserId { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Class { get; set; }
            public DateTime Timestamp { get; set; }
            public RegistrationAction Action { get; set; }

            public ProjectAttendee ToAttendee()
            {
                return new(UserId, FirstName, LastName, Class);
            }
        }

        [JsonConverter(typeof(StringEnumConverter), /* camelCaseText */ true)]
        public enum RegistrationAction
        {
            Register,
            Deregister
        }
    }
}
