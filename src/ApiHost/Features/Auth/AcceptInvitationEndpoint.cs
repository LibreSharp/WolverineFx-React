using ApiHost.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Features.Auth;

public record AcceptInvitationRequest(string UserId, string Token, string Password);

public class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public static class AcceptInvitationEndpoint
{
    // Opened from the invitation email (the client's /accept-invitation page): sets the password
    // of the account the admin created, confirms its email (the link proves the person owns it),
    // and signs them in.
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/accept-invitation")]
    public static async Task<Results<Ok<LoginResponse>, ValidationProblem>> Post(
        AcceptInvitationRequest request,
        UserManager<ApplicationUser> userManager,
        TokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null
            || await userManager.HasPasswordAsync(user)
            || !await userManager.VerifyUserTokenAsync(user, TokenProviders.Invitation, TokenProviders.AcceptInvitationPurpose, request.Token))
        {
            return IdentityResultExtensions.ValidationProblem("Token", "This invitation is invalid, has expired, or was already used.");
        }

        // Checks the password rules; also changes the security stamp, so the link can't be reused.
        var result = await userManager.AddPasswordAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return result.ToValidationProblem();
        }

        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        return TypedResults.Ok(await tokenIssuer.IssueAsync(user, cancellationToken));
    }
}
