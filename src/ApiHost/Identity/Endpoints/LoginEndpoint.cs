using System.Globalization;
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
        // (SignIn.RequireConfirmedEmail), the account must not be locked out, and then the
        // password must match. A wrong password counts towards a lockout (lockoutOnFailure).
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return TypedResults.Ok(await TokenIssuing.IssueAsync(user, userManager, tokenService, refreshTokenStore, cancellationToken));
        }

        // While locked out, no password is checked and anyone is told so (Identity's behaviour;
        // registration reveals which emails have accounts anyway).
        if (result.IsLockedOut)
        {
            return LoginProblems.LockedOut(user);
        }

        // An unconfirmed email is refused before the password is checked. Say so only to someone
        // who knows the password, and count a wrong one towards a lockout like any other.
        if (result.IsNotAllowed)
        {
            if (await userManager.IsLockedOutAsync(user))
            {
                return LoginProblems.LockedOut(user);
            }

            if (await userManager.CheckPasswordAsync(user, request.Password))
            {
                return LoginProblems.EmailNotConfirmed();
            }

            await userManager.AccessFailedAsync(user);
        }

        return TypedResults.Unauthorized();
    }
}

// Why a login was refused, as 403 ProblemDetails with a "reason" the client can act on.
public static class LoginProblems
{
    public const string ReasonKey = "reason";

    public static ProblemHttpResult EmailNotConfirmed() => Refused(
        "EmailNotConfirmed", "Confirm your email address first: open the link in the email we sent you.");

    public static ProblemHttpResult LockedOut(ApplicationUser user)
    {
        if (user.IsLockedByAdmin())
        {
            return Refused("LockedOut", "This account has been locked. Contact us if you think this is a mistake.");
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(((user.LockoutEnd ?? DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow).TotalMinutes));
        return Refused("LockedOut", string.Create(CultureInfo.InvariantCulture,
            $"Too many failed attempts: this account is locked. Try again in {minutes} minute{(minutes == 1 ? "" : "s")}, or reset your password."));
    }

    private static ProblemHttpResult Refused(string reason, string title) => TypedResults.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: title,
        extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { [ReasonKey] = reason });
}
