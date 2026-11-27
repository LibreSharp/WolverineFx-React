using System.Security.Claims;

namespace ApiHost.Organisations;

public static class OrganisationClaims
{
    // The claim in the access token that names the organisation the user is working in. Wolverine
    // reads it as the tenant id of every request (Program.cs), so queries of tenanted data (Todos)
    // only ever see that organisation's.
    public const string OrganisationId = "org";

    // Only call from [Authorize] endpoints: every access token carries the claim.
    public static Guid GetOrganisationId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(OrganisationId), out var id)
            ? id
            : throw new InvalidOperationException("Access token has no organisation claim");
}
