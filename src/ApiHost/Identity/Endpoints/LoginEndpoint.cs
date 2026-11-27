using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

public static class LoginEndpoint
{
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/login")]
    public static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ProblemHttpResult>> Post(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService tokenService,
        IRefreshTokenStore refreshTokenStore,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        // Identity's sign-in checks, without its cookie: the email must be confirmed
        // (SignIn.RequireConfirmedEmail) and the account not locked out, then the password.
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return TypedResults.Ok(await TokenIssuing.IssueAsync(user, userManager, tokenService, refreshTokenStore, cancellationToken));
        }

        // An unconfirmed email is refused before the password is checked. Say so only to someone
        // who knows the password, so it doesn't reveal which emails have accounts.
        if (result.IsNotAllowed && await userManager.CheckPasswordAsync(user, request.Password))
        {
            return LoginProblems.EmailNotConfirmed();
        }

        return TypedResults.Unauthorized();
    }
}

// Why a login was refused, as 403 ProblemDetails with a "reason" the client can act on.
public static class LoginProblems
{
    public const string ReasonKey = "reason";

    public static ProblemHttpResult EmailNotConfirmed() => TypedResults.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Confirm your email address first: open the link in the email we sent you.",
        extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { [ReasonKey] = "EmailNotConfirmed" });
}
