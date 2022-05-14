using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace Prowo.BlazorServer.Database
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class DbProject
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string OrganizerId { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int MaxParticipants { get; set; }
    }
}
