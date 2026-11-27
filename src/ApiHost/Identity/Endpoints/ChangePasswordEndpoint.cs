using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}

public static class ChangePasswordEndpoint
{
    // Changing the password signs out every other session (all refresh tokens are revoked) and
    // returns new tokens, so the session that made the change stays signed in.
    [Authorize]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/account/password")]
    public static async Task<Results<Ok<LoginResponse>, ValidationProblem, UnauthorizedHttpResult>> Post(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenStore refreshTokenStore,
        TokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        // Checks the current password and the new password's strength rules.
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return result.ToValidationProblem();
        }

        await refreshTokenStore.RevokeAllForUserAsync(user.Id, cancellationToken);
        return TypedResults.Ok(await tokenIssuer.IssueAsync(user, cancellationToken));
    }
}
