using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ApiHost.Identity;

// The tokens every way of logging in returns (LoginEndpoint, RefreshEndpoint, ...).
public record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

// Signs a user in: creates a new access token and a new stored refresh token, a fresh session.
// Used by every way of logging in, and by change password (which ends every other session first).
public class TokenIssuer(
    UserManager<ApplicationUser> userManager,
    JwtTokenService tokenService,
    IRefreshTokenStore refreshTokenStore,
    IOptionsSnapshot<AccountOptions> accountOptions)
{
    public async Task<LoginResponse> IssueAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // An email listed in Accounts:AdminEmails that registered after startup.
        await userManager.MakeAdminIfConfiguredAsync(user, accountOptions.Value);

        var roles = await userManager.GetRolesAsync(user);
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
