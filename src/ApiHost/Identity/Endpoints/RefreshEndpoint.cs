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

        if (existing.RevokedAtUtc is not null)
        {
            // Reuse of an already-rotated-away token: treat as compromised and revoke the whole chain.
            await refreshTokenStore.RevokeAllForUserAsync(existing.UserId, cancellationToken);
            return TypedResults.Unauthorized();
        }

        if (!existing.IsActive)
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(existing.UserId);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var roles = await userManager.GetUserRolesAsync(user);
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
