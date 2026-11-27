using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record MeResponse(string UserId, string Email, string? DisplayName, IReadOnlyList<string> Roles)
{
    public static async Task<MeResponse> FromAsync(ApplicationUser user, UserManager<ApplicationUser> userManager)
        => new(user.Id, user.Email ?? string.Empty, user.DisplayName, [.. await userManager.GetRolesAsync(user)]);
}

public static class MeEndpoint
{
    // Read from the database rather than the token's claims, so a changed email, display name or
    // role shows straight away.
    [Authorize]
    [WolverineGet("/api/auth/me")]
    public static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> Get(
        ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await MeResponse.FromAsync(user, userManager));
    }
}
