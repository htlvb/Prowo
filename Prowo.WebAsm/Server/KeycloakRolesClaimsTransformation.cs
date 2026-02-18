using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

class KeycloakRolesClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is ClaimsIdentity identity)
        {
            if (identity.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") is Claim nameIdentifier)
            {
                identity.AddClaim(new Claim("oid", nameIdentifier.Value));
            }
            var realmAccessClaim = identity.FindFirst("resource_access");
            if (realmAccessClaim != null)
            {
                var realmAccess = JsonNode.Parse(realmAccessClaim.Value);
                string[] roles = JsonSerializer.Deserialize<string[]>(realmAccess?["prowo"]?["roles"]) ?? [];
                foreach (var role in roles)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }
        }
        return Task.FromResult(principal);
    }
}