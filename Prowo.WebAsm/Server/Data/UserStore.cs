using Microsoft.Graph;
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

        public async IAsyncEnumerable<ProjectOrganizer> GetOrganizerCandidates()
        {
            var userPageRequest = graphServiceClient.Groups[organizerGroupId].Members.Request().Top(999);
            while (userPageRequest != null)
            {
                var userPage = await userPageRequest.GetAsync();
                var users = userPage
                    .OfType<User>()
                    .Select(v => new ProjectOrganizer(v.Id, v.GivenName, v.Surname, Regex.Replace(v.UserPrincipalName, "@.*$", "")));
                foreach (var user in users)
                {
                    yield return user;
                }

                userPageRequest = userPage.NextPageRequest;
            }
        }

        public async Task<ProjectAttendee> GetSelfAsProjectAttendee()
        {
            var user = await graphServiceClient.Me.Request().Select("id,givenName,surname,department,userPrincipalName").GetAsync();
            return new ProjectAttendee(user.Id, user.GivenName, user.Surname, user.Department, user.UserPrincipalName);
        }

        public async IAsyncEnumerable<ProjectAttendee> GetAttendeeCandidates()
        {
            var userPageRequest = graphServiceClient.Groups[attendeeGroupId].Members.Request().Select("id,givenName,surname,department,userPrincipalName");
            while (userPageRequest != null)
            {
                var userPage = await userPageRequest.GetAsync();
                var users = userPage
                    .OfType<User>()
                    .Where(v => v.Department != null)
                    .Select(v => new ProjectAttendee(v.Id, v.GivenName, v.Surname, v.Department, v.UserPrincipalName));
                foreach (var user in users)
                {
                    yield return user;
                }

                userPageRequest = userPage.NextPageRequest;
            }
        }
    }
}
