#if (Saas)
using ApiHost.Organisations;
#endif
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ApiHost.Identity;

// The tokens every way of logging in returns (LoginEndpoint, RefreshEndpoint, ...).
public record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

// Signs a user in: creates a new access token and a new stored refresh token. Used by every way
// of logging in, by change password (which ends every other session first), and to refresh a
// session's tokens.
public class TokenIssuer(
    UserManager<ApplicationUser> userManager,
    JwtTokenService tokenService,
    IRefreshTokenStore refreshTokenStore,
#if (Saas)
    OrganisationAccess organisations,
#endif
    IOptionsSnapshot<AccountOptions> accountOptions)
{
    // A new session.
    public Task<LoginResponse> IssueAsync(ApplicationUser user, CancellationToken cancellationToken)
#if (Saas)
        => IssueAsync(user, replacing: null, organisationId: null, cancellationToken);
#else
        => IssueAsync(user, replacing: null, cancellationToken);
#endif

    // replacing: the refresh token that the new one replaces in the same session (rotation); it
    // is revoked, and remembered as replaced so that reusing it is detected (RefreshEndpoint).
#if (Saas)
    // organisationId: the organisation to work in, when switching. Otherwise the session keeps
    // its organisation, or a new session starts in the one the user used last.
    public async Task<LoginResponse> IssueAsync(ApplicationUser user, RefreshToken? replacing, Guid? organisationId, CancellationToken cancellationToken)
#else
    public async Task<LoginResponse> IssueAsync(ApplicationUser user, RefreshToken? replacing, CancellationToken cancellationToken)
#endif
    {
        // An email listed in Accounts:AdminEmails that registered after startup.
        await userManager.MakeAdminIfConfiguredAsync(user, accountOptions.Value);

        var roles = await userManager.GetRolesAsync(user);
#if (Saas)
        var organisation = await organisations.ChooseAsync(user, organisationId ?? replacing?.OrganisationId, cancellationToken);
        var (accessToken, expiresAtUtc) = tokenService.CreateAccessToken(user, roles, organisation);
#else
        var (accessToken, expiresAtUtc) = tokenService.CreateAccessToken(user, roles);
#endif

        var refreshTokenPlaintext = JwtTokenService.GenerateRefreshTokenPlaintext();
        var refreshTokenHash = JwtTokenService.HashToken(refreshTokenPlaintext);
        if (replacing is not null)
        {
            await refreshTokenStore.RevokeAsync(replacing, refreshTokenHash, cancellationToken);
        }

        await refreshTokenStore.StoreAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = tokenService.RefreshTokenExpiry(),
#if (Saas)
            OrganisationId = organisation,
#endif
        }, cancellationToken);

        return new LoginResponse(accessToken, refreshTokenPlaintext, expiresAtUtc);
    }
}
