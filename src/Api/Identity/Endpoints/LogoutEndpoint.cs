using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace Api.Identity.Endpoints;

public record LogoutRequest(string RefreshToken);

public static class LogoutEndpoint
{
    [WolverinePost("/api/auth/logout")]
    public static async Task<NoContent> Post(
        LogoutRequest request,
        IRefreshTokenStore refreshTokenStore,
        CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.HashToken(request.RefreshToken);
        var existing = await refreshTokenStore.FindByHashAsync(hash, cancellationToken);
        if (existing is not null && existing.RevokedAtUtc is null)
        {
            await refreshTokenStore.RevokeAsync(existing, null, cancellationToken);
        }

        return TypedResults.NoContent();
    }
}
