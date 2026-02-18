using System.Text.RegularExpressions;
using Keycloak.AdminApi.Models;
using Microsoft.Identity.Web;

namespace Prowo.WebAsm.Server.Data
{
    public class KeycloakUserStore(
        string realmName,
        string organizerGroupId,
        string attendeeGroupId,
        Regex includedClasses,
        KeycloakAdminApiClientFactory keycloakAdminApiClientFactory,
        IHttpContextAccessor httpContextAccessor) : IUserStore
    {
        public async IAsyncEnumerable<ProjectOrganizer> GetOrganizerCandidates()
        {
            var keycloakAdminApiClient = await keycloakAdminApiClientFactory.CreateClient();
            var users = await keycloakAdminApiClient.Admin
                .Realms[realmName]
                .Groups[organizerGroupId]
                .Members
                .GetAsync();
            if (users == null)
            {
                yield break;
            }
            var organizers = users
                .Select(v =>
                {
                    if (v.Id == null || v.Username == null)
                    {
                        return null;
                    }

                    return new ProjectOrganizer(
                        v.Id,
                        v.FirstName ?? "",
                        v.LastName ?? "",
                        v.Username.ToUpper()
                    );
                })
                .OfType<ProjectOrganizer>();
            foreach (var organizer in organizers)
            {
                yield return organizer;
            }
        }

        public async Task<ProjectAttendee> GetSelfAsProjectAttendee()
        {
            var keycloakAdminApiClient = await keycloakAdminApiClientFactory.CreateClient();
            var userId = httpContextAccessor.HttpContext?.User.GetObjectId() ?? throw new Exception("Can't get user id without HTTP context or user has no id.");
            var user = await keycloakAdminApiClient.Admin.Realms[realmName].Users[userId].GetAsync();
            var userGroups = await keycloakAdminApiClient.Admin.Realms[realmName].Users[userId].Groups.GetAsync();
            var userGroup = userGroups?.FirstOrDefault()?.Name;
            if (user == null || user.Id == null || user.Email == null || userGroup == null)
            {
                throw new Exception($"Keycloak user not found or has incomplete data (Id = {user?.Id}, Email = {user?.Email}, Group = {userGroup})");
            }
            return new ProjectAttendee(user.Id, user.FirstName ?? "", user.LastName ?? "", userGroup, user.Email);
        }

        public async IAsyncEnumerable<ProjectAttendee> GetAttendeeCandidates()
        {
            var keycloakAdminApiClient = await keycloakAdminApiClientFactory.CreateClient();
            var userGroups = await keycloakAdminApiClient.Admin
                .Realms[realmName]
                .Groups[attendeeGroupId]
                .Children
                .GetAsync(v => v.QueryParameters.Max = -1);
            if (userGroups == null)
            {
                yield break;
            }
            var attendees = userGroups
                .ToAsyncEnumerable()
                .SelectMany(FetchGroupMembers);
            await foreach (var attendee in attendees)
            {
                yield return attendee;
            }

            async IAsyncEnumerable<ProjectAttendee> FetchGroupMembers(GroupRepresentation userGroup)
            {
                if (userGroup.Name == null || !includedClasses.IsMatch(userGroup.Name))
                {
                    yield break;
                }
                var users = await keycloakAdminApiClient.Admin
                    .Realms[realmName]
                    .Groups[userGroup.Id]
                    .Members
                    .GetAsync(v => v.QueryParameters.Max = -1);
                if (users == null)
                {
                    yield break;
                }
                var attendees = users
                    .Select(v =>
                    {
                        if (v.Id == null || v.Email == null)
                        {
                            return null;
                        }
                        return new ProjectAttendee(
                            v.Id,
                            v.FirstName ?? "",
                            v.LastName ?? "",
                            userGroup.Name,
                            v.Email
                        );
                    })
                    .OfType<ProjectAttendee>();
                foreach (var attendee in attendees)
                {
                    yield return attendee;
                }
            };
        }
    }
}
