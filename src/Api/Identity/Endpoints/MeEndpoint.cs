using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.JsonWebTokens;
using Wolverine.Http;

namespace Api.Identity.Endpoints;

public record MeResponse(string UserId, string? Email);

public static class MeEndpoint
{
    [Authorize]
    [WolverineGet("/api/auth/me")]
    public static Ok<MeResponse> Get(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(JwtRegisteredClaimNames.Email);

        return TypedResults.Ok(new MeResponse(userId ?? string.Empty, email));
    }
}
