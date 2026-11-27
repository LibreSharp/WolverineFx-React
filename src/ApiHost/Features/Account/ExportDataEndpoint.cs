using System.Security.Claims;
using ApiHost.DataExport;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Features.Account;

public static class ExportDataEndpoint
{
    // Everything stored about the signed-in user, as a JSON file (DataExport/).
    [Authorize]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverineGet("/api/account/export")]
    public static Task<IResult> Get(ClaimsPrincipal user, PersonalDataExport export, CancellationToken cancellationToken)
        => export.FileAsync(user.GetUserId(), cancellationToken);
}
