using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}

public static class ResetPasswordEndpoint
{
    // Sets a new password using the token from the reset email, and signs out every session.
    // Opening the emailed link proves the user owns the address, so it also confirms the email
    // (someone who lost the confirmation email can get in this way) and lifts a lockout from
    // failed logins.
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/reset-password")]
    public static async Task<Results<NoContent, ValidationProblem>> Post(
        ResetPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenStore refreshTokenStore,
        CancellationToken cancellationToken)
    {
        // Same answer for an unknown email as for a bad token, so this doesn't reveal accounts.
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return IdentityResultExtensions.ValidationProblem("Token", "This reset link is invalid or has expired.");
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? IdentityResultExtensions.ValidationProblem("Token", "This reset link is invalid or has expired.")
                : result.ToValidationProblem();
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        await userManager.ClearFailedLoginLockoutAsync(user);

        await refreshTokenStore.RevokeAllForUserAsync(user.Id, cancellationToken);
        return TypedResults.NoContent();
    }
}
