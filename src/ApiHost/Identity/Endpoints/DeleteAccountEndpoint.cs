using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record DeleteAccountRequest(string Password);

public class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}

public static class DeleteAccountEndpoint
{
    // Permanently deletes the account. The password is required again, so a stolen access
    // token alone can't delete it. The user's data in other features is deleted by their
    // AccountDeleted handlers.
    [Authorize]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/account/delete")]
    public static async Task<(Results<NoContent, ValidationProblem, UnauthorizedHttpResult>, OutgoingMessages)> Post(
        DeleteAccountRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenStore refreshTokenStore,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user is null)
        {
            return (TypedResults.Unauthorized(), []);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            return (IdentityResultExtensions.ValidationProblem("Password", "The password is incorrect."), []);
        }

        await refreshTokenStore.RevokeAllForUserAsync(user.Id, cancellationToken);
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return (result.ToValidationProblem(), []);
        }

        return (TypedResults.NoContent(), [new AccountDeleted(user.Id)]);
    }
}
