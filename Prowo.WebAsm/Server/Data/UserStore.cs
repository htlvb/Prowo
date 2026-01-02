using Microsoft.Graph;
using Models = Microsoft.Graph.Models;
using System.Text.RegularExpressions;

namespace Prowo.WebAsm.Server.Data
{
    public class UserStore : IUserStore
    {
        private readonly string organizerGroupId;
        private readonly string attendeeGroupId;
        private readonly GraphServiceClient graphServiceClient;

        public UserStore(
            string organizerGroupId,
            string attendeeGroupId,
            GraphServiceClient graphServiceClient)
        {
            this.organizerGroupId = organizerGroupId;
            this.attendeeGroupId = attendeeGroupId;
            this.graphServiceClient = graphServiceClient;
        }

        public IAsyncEnumerable<ProjectOrganizer> GetOrganizerCandidates()
        {
            return graphServiceClient
                .ReadAll<Models.User, Models.UserCollectionResponse>(
                    graphServiceClient.Groups[organizerGroupId].Members.GraphUser.GetAsync()
                )
                .Select(v =>
                {
                    if (v.Id == null || v.UserPrincipalName == null)
                    {
                        return null;
                    }

                    return new ProjectOrganizer(
                        v.Id,
                        v.GivenName ?? "",
                        v.Surname ?? "",
                        Regex.Replace(v.UserPrincipalName, "@.*$", ""));
                })
                .OfType<ProjectOrganizer>();
        }

        public async Task<ProjectAttendee> GetSelfAsProjectAttendee()
        {
            var user = await graphServiceClient.Me.GetAsync(v =>
                v.QueryParameters.Select = ["id", "givenName", "surname", "department", "userPrincipalName"]);
            if (user == null || user.Id == null || user.Department == null || user.UserPrincipalName == null)
            {
                throw new Exception($"Graph user not found or has incomplete data (Id = {user?.Id}, Department = {user?.Department}, UserPrincipalName = {user?.UserPrincipalName})");
            }
            return new ProjectAttendee(user.Id, user.GivenName ?? "", user.Surname ?? "", user.Department, user.UserPrincipalName);
        }

        public IAsyncEnumerable<ProjectAttendee> GetAttendeeCandidates()
        {
            return graphServiceClient
                .ReadAll<Models.User, Models.UserCollectionResponse>(
                    graphServiceClient.Groups[attendeeGroupId].Members.GraphUser.GetAsync(v =>
                        v.QueryParameters.Select = ["id", "givenName", "surname", "department", "userPrincipalName"]
                    ))
                .Select(v =>
                {
                    if (v.Id == null || v.Department == null || v.UserPrincipalName == null)
                    {
                        return null;
                    }
                    return new ProjectAttendee(v.Id, v.GivenName ?? "", v.Surname ?? "", v.Department, v.UserPrincipalName);
                })
                .OfType<ProjectAttendee>();
        }
    }
}
