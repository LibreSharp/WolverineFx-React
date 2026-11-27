using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record RefreshRequest(string RefreshToken);

public record RefreshResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

public static class RefreshEndpoint
{
    [WolverinePost("/api/auth/refresh")]
    public static async Task<Results<Ok<RefreshResponse>, UnauthorizedHttpResult>> Post(
        RefreshRequest request,
        UserManager<ApplicationUser> userManager,
        JwtTokenService tokenService,
        IRefreshTokenStore refreshTokenStore,
        CancellationToken cancellationToken)
    {
        var incomingHash = JwtTokenService.HashToken(request.RefreshToken);
        var existing = await refreshTokenStore.FindByHashAsync(incomingHash, cancellationToken);

        if (existing is null)
        {
            return TypedResults.Unauthorized();
        }

        if (existing.ReplacedByTokenHash is not null)
        {
            // Reuse of a token that was already rotated away (replaced by a newer one): someone
            // may have copied it, so treat it as compromised and revoke all the user's tokens.
            await refreshTokenStore.RevokeAllForUserAsync(existing.UserId, cancellationToken);
            return TypedResults.Unauthorized();
        }

        // Revoked without a replacement (logout, password change or reset) or expired: refuse
        // it, but don't sign out the user's other sessions.
        if (!existing.IsActive)
        {
            return TypedResults.Unauthorized();
        }

        // A locked-out account (failed logins, or locked by an admin) gets no new access tokens,
        // so its sessions end within one access token lifetime.
        var user = await userManager.FindByIdAsync(existing.UserId);
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return TypedResults.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresAtUtc) = tokenService.CreateAccessToken(user, roles);

        var newRefreshTokenPlaintext = JwtTokenService.GenerateRefreshTokenPlaintext();
        var newHash = JwtTokenService.HashToken(newRefreshTokenPlaintext);

        await refreshTokenStore.RevokeAsync(existing, newHash, cancellationToken);
        await refreshTokenStore.StoreAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = tokenService.RefreshTokenExpiry()
        }, cancellationToken);

        return TypedResults.Ok(new RefreshResponse(accessToken, newRefreshTokenPlaintext, expiresAtUtc));
    }
}
