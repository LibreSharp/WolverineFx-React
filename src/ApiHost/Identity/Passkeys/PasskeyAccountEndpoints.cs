using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Wolverine.Http;

namespace ApiHost.Identity.Passkeys;

// Id is the credential ID in base64url, as used in the URLs below.
public record PasskeySummary(string Id, string Name, DateTimeOffset CreatedAt);

public record AddPasskeyRequest(string CredentialJson, string State, string? Name);

public class AddPasskeyRequestValidator : AbstractValidator<AddPasskeyRequest>
{
    public AddPasskeyRequestValidator()
    {
        RuleFor(x => x.CredentialJson).NotEmpty();
        RuleFor(x => x.State).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(100);
    }
}

public record RenamePasskeyRequest(string Name);

public class RenamePasskeyRequestValidator : AbstractValidator<RenamePasskeyRequest>
{
    public RenamePasskeyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

// The signed-in user's passkeys, for Settings. Adding one takes two steps: the API sends creation
// options, the browser creates the passkey (navigator.credentials.create), and the API checks
// and stores its public key.
public static class PasskeyAccountEndpoints
{
    private const string Purpose = "Register";

    [Authorize]
    [WolverinePost("/api/account/passkeys/options")]
    public static async Task<Results<Ok<PasskeyOptionsResponse>, UnauthorizedHttpResult>> Options(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IPasskeyHandler<ApplicationUser> passkeys,
        PasskeyStates states,
        HttpContext httpContext)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user?.Email is null)
        {
            return TypedResults.Unauthorized();
        }

        var options = await passkeys.MakeCreationOptionsAsync(
            new PasskeyUserEntity { Id = user.Id, Name = user.Email, DisplayName = user.DisplayName ?? user.Email },
            httpContext);
        return TypedResults.Ok(new PasskeyOptionsResponse(options.CreationOptionsJson, states.Protect(options.AttestationState ?? string.Empty, Purpose, user.Id)));
    }

    [Authorize]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/account/passkeys")]
    public static async Task<Results<Created, ValidationProblem, UnauthorizedHttpResult>> Add(
        AddPasskeyRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IPasskeyHandler<ApplicationUser> passkeys,
        PasskeyStates states,
        HttpContext httpContext)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var state = states.Unprotect(request.State, Purpose, user.Id);
        if (state is null)
        {
            return IdentityResultExtensions.ValidationProblem("State", "That took too long. Please try again.");
        }

        var result = await passkeys.PerformAttestationAsync(new PasskeyAttestationContext
        {
            CredentialJson = request.CredentialJson,
            AttestationState = state,
            HttpContext = httpContext,
        });
        if (!result.Succeeded || result.UserEntity.Id != user.Id)
        {
            return IdentityResultExtensions.ValidationProblem("Passkey", $"The passkey couldn't be added: {result.Failure?.Message}");
        }

        result.Passkey.Name = string.IsNullOrWhiteSpace(request.Name) ? "Passkey" : request.Name.Trim();
        var added = await userManager.AddOrUpdatePasskeyAsync(user, result.Passkey);
        return added.Succeeded ? TypedResults.Created() : added.ToValidationProblem();
    }

    [Authorize]
    [WolverineGet("/api/account/passkeys")]
    public static async Task<Results<Ok<IReadOnlyList<PasskeySummary>>, UnauthorizedHttpResult>> List(
        ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var passkeys = await userManager.GetPasskeysAsync(user);
        return TypedResults.Ok<IReadOnlyList<PasskeySummary>>([.. passkeys
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PasskeySummary(WebEncoders.Base64UrlEncode(p.CredentialId), p.Name ?? "Passkey", p.CreatedAt))]);
    }

    [Authorize]
    [WolverinePut("/api/account/passkeys/{id}")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> Rename(
        string id, RenamePasskeyRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var (user, passkey) = await FindAsync(id, principal, userManager);
        if (user is null || passkey is null)
        {
            return TypedResults.NotFound();
        }

        passkey.Name = request.Name.Trim();
        var result = await userManager.AddOrUpdatePasskeyAsync(user, passkey);
        return result.Succeeded ? TypedResults.NoContent() : result.ToValidationProblem();
    }

    // Refused if it's the only way left to sign in (see SignInMethods).
    [Authorize]
    [WolverineDelete("/api/account/passkeys/{id}")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> Remove(
        string id, ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var (user, passkey) = await FindAsync(id, principal, userManager);
        if (user is null || passkey is null)
        {
            return TypedResults.NotFound();
        }

        if (await userManager.CountSignInMethodsAsync(user) <= 1)
        {
            return IdentityResultExtensions.ValidationProblem("Passkey",
                "This is the only way you can sign in. Set a password first (Forgot your password? on the login page).");
        }

        var result = await userManager.RemovePasskeyAsync(user, passkey.CredentialId);
        return result.Succeeded ? TypedResults.NoContent() : result.ToValidationProblem();
    }

    private static async Task<(ApplicationUser? User, UserPasskeyInfo? Passkey)> FindAsync(
        string id, ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user is null)
        {
            return (null, null);
        }

        byte[] credentialId;
        try
        {
            credentialId = WebEncoders.Base64UrlDecode(id);
        }
        catch (FormatException)
        {
            return (user, null);
        }

        return (user, await userManager.GetPasskeyAsync(user, credentialId));
    }
}
