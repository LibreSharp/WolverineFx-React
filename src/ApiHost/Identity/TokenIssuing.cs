using ApiHost.Identity.Endpoints;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity;

public static class TokenIssuing
{
    // Creates a new access token and a new stored refresh token for the user: a fresh signed-in
    // session. Used by login and by change password (which ends every other session first).
    public static async Task<LoginResponse> IssueAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        JwtTokenService tokenService,
        IRefreshTokenStore refreshTokenStore,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetUserRolesAsync(user);
        var (accessToken, expiresAtUtc) = tokenService.CreateAccessToken(user, roles);

        var refreshTokenPlaintext = JwtTokenService.GenerateRefreshTokenPlaintext();
        await refreshTokenStore.StoreAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = JwtTokenService.HashToken(refreshTokenPlaintext),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = tokenService.RefreshTokenExpiry()
        }, cancellationToken);

        return new LoginResponse(accessToken, refreshTokenPlaintext, expiresAtUtc);
    }
}
