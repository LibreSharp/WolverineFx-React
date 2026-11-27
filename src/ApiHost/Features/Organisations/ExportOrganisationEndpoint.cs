using System.Security.Claims;
using ApiHost.DataExport;
using ApiHost.Identity;
using ApiHost.Organisations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Features.Organisations;

public static class ExportOrganisationEndpoint
{
    // Everything stored for the current organisation, as a JSON file (DataExport/). Owners only:
    // it includes every member's email address.
    [Authorize]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverineGet("/api/organisation/export")]
    public static async Task<IResult> Get(ClaimsPrincipal user, IOrganisationStore organisations, OrganisationDataExport export, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        return (await organisations.FindMembershipAsync(organisationId, user.GetUserId(), cancellationToken))?.Role == OrganisationRoles.Owner
            ? await export.FileAsync(organisationId, cancellationToken)
            : TypedResults.Forbid();
    }
}
