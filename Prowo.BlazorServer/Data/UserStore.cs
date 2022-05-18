using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Prowo.BlazorServer.Data
{
    public class UserStore
    {
        private readonly string organizerGroupId;
        private readonly GraphServiceClient graphServiceClient;
        private readonly MicrosoftIdentityConsentAndConditionalAccessHandler consentHandler;

        public UserStore(
            string organizerGroupId,
            GraphServiceClient graphServiceClient,
            MicrosoftIdentityConsentAndConditionalAccessHandler consentHandler)
        {
            this.organizerGroupId = organizerGroupId;
            this.graphServiceClient = graphServiceClient;
            this.consentHandler = consentHandler;
        }

        public async IAsyncEnumerable<DbUser> GetOrganizerCandidates()
        {
            var userPageRequest = graphServiceClient.Groups[organizerGroupId].Members.Request();
            while (userPageRequest != null)
            {
                var userPage = await HandleConsentRequiredException(userPageRequest, r => r.GetAsync());
                var users = userPage
                    .OfType<User>()
                    .OrderBy(v => v.UserPrincipalName)
                    .Select(v => new DbUser(v.Id, $"{v.DisplayName} ({Regex.Replace(v.UserPrincipalName, "@.*$", "")})"));
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
