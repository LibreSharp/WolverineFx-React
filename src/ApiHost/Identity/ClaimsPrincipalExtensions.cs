using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ApiHost.Identity;

public static class ClaimsPrincipalExtensions
{
    // Only call from [Authorize] endpoints; an authenticated JWT always carries a sub claim.
    public static string GetUserId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated user has no subject claim");
    }
}
