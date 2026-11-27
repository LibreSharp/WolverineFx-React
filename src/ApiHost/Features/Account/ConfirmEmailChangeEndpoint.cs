using ApiHost.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Features.Account;

public record ConfirmEmailChangeRequest(string UserId, string Email, string Token);

public class ConfirmEmailChangeRequestValidator : AbstractValidator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
    }
}

public static class ConfirmEmailChangeEndpoint
{
    // Opened from the link sent to the new address when a user changes their email in Settings
    // (the client's /confirm-email-change page). Needs no login: the token proves the request.
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/confirm-email-change")]
    public static async Task<Results<NoContent, ValidationProblem>> Post(
        ConfirmEmailChangeRequest request, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return IdentityResultExtensions.ValidationProblem("Token", "This confirmation link is invalid or has expired.");
        }

        // Checks the token (made for this exact address) and changes the email, which also
        // marks it confirmed. Fails if another account has taken the address in the meantime.
        var result = await userManager.ChangeEmailAsync(user, request.Email, request.Token);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? IdentityResultExtensions.ValidationProblem("Token", "This confirmation link is invalid or has expired.")
                : result.ToValidationProblem();
        }

        // The email is also the user name used to log in.
        result = await userManager.SetUserNameAsync(user, request.Email);
        return result.Succeeded ? TypedResults.NoContent() : result.ToValidationProblem();
    }
}
