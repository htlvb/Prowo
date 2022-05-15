using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace Prowo.BlazorServer.Database
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
        public int MaxParticipants { get; set; }
    }
}
