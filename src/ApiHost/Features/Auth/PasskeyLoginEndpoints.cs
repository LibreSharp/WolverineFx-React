using ApiHost.Identity;
using ApiHost.Identity.Passkeys;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Features.Auth;

public record PasskeyLoginRequest(string CredentialJson, string State);

public class PasskeyLoginRequestValidator : AbstractValidator<PasskeyLoginRequest>
{
    public PasskeyLoginRequestValidator()
    {
        RuleFor(x => x.CredentialJson).NotEmpty();
        RuleFor(x => x.State).NotEmpty();
    }
}

// Logging in with a passkey, in two steps: the API sends a challenge, the browser asks the
// passkey to sign it (navigator.credentials.get), and the API checks the signature.
public static class PasskeyLoginEndpoints
{
    private const string Purpose = "Login";

    // No user is named: the browser offers the passkeys it has for this site (discoverable
    // credentials), so the person doesn't type their email.
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/passkey/options")]
    public static async Task<PasskeyOptionsResponse> Options(
        IPasskeyHandler<ApplicationUser> passkeys, PasskeyStates states, HttpContext httpContext)
    {
        var options = await passkeys.MakeRequestOptionsAsync(null, httpContext);
        return new PasskeyOptionsResponse(options.RequestOptionsJson, states.Protect(options.AssertionState ?? string.Empty, Purpose));
    }

    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/passkey/login")]
    public static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ProblemHttpResult>> Login(
        PasskeyLoginRequest request,
        IPasskeyHandler<ApplicationUser> passkeys,
        PasskeyStates states,
        UserManager<ApplicationUser> userManager,
        TokenIssuer tokenIssuer,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var state = states.Unprotect(request.State, Purpose);
        if (state is null)
        {
            return TypedResults.Unauthorized();
        }

        // Checks the signature against the stored public key, the challenge, and the origin.
        var result = await passkeys.PerformAssertionAsync(new PasskeyAssertionContext
        {
            CredentialJson = request.CredentialJson,
            AssertionState = state,
            HttpContext = httpContext,
        });
        if (!result.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        var user = result.User;
        if (await userManager.IsLockedOutAsync(user))
        {
            return LoginProblems.LockedOut(user);
        }

        // Saves the passkey's new signature counter (Identity uses it to spot cloned keys).
        await userManager.AddOrUpdatePasskeyAsync(user, result.Passkey);
        return TypedResults.Ok(await tokenIssuer.IssueAsync(user, cancellationToken));
    }
}
