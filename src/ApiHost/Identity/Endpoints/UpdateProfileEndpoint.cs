using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record UpdateProfileRequest(string Email, string? DisplayName);

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.DisplayName).MaximumLength(100);
    }
}

public static class UpdateProfileEndpoint
{
    // The email is also the user name used to log in. It changes straight away: there's no
    // confirmation email, since the template doesn't send email outside password reset.
    [Authorize]
    [WolverinePut("/api/account/profile")]
    public static async Task<Results<Ok<MeResponse>, ValidationProblem, UnauthorizedHttpResult>> Put(
        UpdateProfileRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        user.Email = request.Email;
        user.UserName = request.Email;
        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();

        // UpdateAsync validates the user (for example, the email must not belong to another
        // account) and updates the normalized email and user name used for lookups.
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? TypedResults.Ok(MeResponse.From(user)) : result.ToValidationProblem();
    }
}
