using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace Prowo.Web.Data
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class DbProject
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string OrganizerId { get; set; }
        public string[] CoOrganizerIds { get; set; }
        public string Date { get; set; }
        public string StartTime { get; set; }
        public string? EndTime { get; set; }
        public int MaxAttendees { get; set; }
        public RegistrationEvent[] RegistrationEvents { get; set; }

        public List<ProjectAttendee> CalculateActualAttendees()
        {
            List<ProjectAttendee> result = new();
            foreach (var entry in RegistrationEvents)
            {
                if (entry.Action == RegistrationAction.Register)
                {
                    result.Add(entry.ToAttendee());
                }
                else if (entry.Action == RegistrationAction.Deregister)
                {
                    result.RemoveAll(v => v.UserId == entry.UserId);
                }
            }
            return result;
        }

        [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
        public class RegistrationEvent
        {
            public string UserId { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Class { get; set; }
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
