using Microsoft.Graph;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Prowo.Web.Data
{
    public class UserStore
    {
        private readonly string organizerGroupId;
        private readonly string attendeeGroupId;
        private readonly GraphServiceClient graphServiceClient;
        private readonly MicrosoftIdentityConsentAndConditionalAccessHandler consentHandler;

        public UserStore(
            string organizerGroupId,
            string attendeeGroupId,
            GraphServiceClient graphServiceClient,
            MicrosoftIdentityConsentAndConditionalAccessHandler consentHandler)
        {
            this.organizerGroupId = organizerGroupId;
            this.attendeeGroupId = attendeeGroupId;
            this.graphServiceClient = graphServiceClient;
            this.consentHandler = consentHandler;
        }

        public async IAsyncEnumerable<OrganizerCandidate> GetOrganizerCandidates()
        {
            var userPageRequest = graphServiceClient.Groups[organizerGroupId].Members.Request();
            while (userPageRequest != null)
            {
                var userPage = await HandleConsentRequiredException(userPageRequest, r => r.GetAsync());
                var users = userPage
                    .OfType<User>()
                    .Select(v => new OrganizerCandidate(v.Id, $"{v.DisplayName} ({Regex.Replace(v.UserPrincipalName, "@.*$", "")})"));
                foreach (var user in users)
                {
                    yield return user;
                }

                userPageRequest = userPage.NextPageRequest;
            }
        }

        public async Task<ProjectAttendee> GetSelfAsProjectAttendee()
        {
            var user = await graphServiceClient.Me.Request().Select("id,givenName,surname,department").GetAsync();
            return new ProjectAttendee(user.Id, user.GivenName, user.Surname, user.Department);
        }

        public async IAsyncEnumerable<ProjectAttendee> GetAttendeeCandidates()
        {
            var userPageRequest = graphServiceClient.Groups[attendeeGroupId].Members.Request().Select("id,givenName,surname,department");
            while (userPageRequest != null)
            {
                var userPage = await HandleConsentRequiredException(userPageRequest, r => r.GetAsync());
                var users = userPage
                    .OfType<User>()
                    .Select(v => new ProjectAttendee(v.Id, v.GivenName, v.Surname, v.Department));
                foreach (var user in users)
                {
                    yield return user;
                }

                userPageRequest = userPage.NextPageRequest;
            }
        }

        private async Task<TOut> HandleConsentRequiredException<TIn, TOut>(TIn data, Func<TIn, Task<TOut>> fn)
        {
            try
            {
                return await fn(data);
            }
            catch (Exception ex)
            {
                consentHandler.HandleException(ex);
                throw new LoginRequiredException("Login is required.", ex);
            }
        }
    }
}
