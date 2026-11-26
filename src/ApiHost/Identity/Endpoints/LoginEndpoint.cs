using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

public static class LoginEndpoint
{
    [WolverinePost("/api/auth/login")]
    public static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> Post(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        JwtTokenService tokenService,
        IRefreshTokenStore refreshTokenStore,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return TypedResults.Unauthorized();
        }

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

        return TypedResults.Ok(new LoginResponse(accessToken, refreshTokenPlaintext, expiresAtUtc));
    }
}
