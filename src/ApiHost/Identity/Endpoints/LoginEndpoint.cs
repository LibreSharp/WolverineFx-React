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

        return TypedResults.Ok(await TokenIssuing.IssueAsync(user, userManager, tokenService, refreshTokenStore, cancellationToken));
    }
}
