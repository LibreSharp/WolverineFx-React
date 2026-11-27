using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using ApiHost.Identity.Email;
using Microsoft.AspNetCore.Identity;
using Wolverine;
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

// ConfirmationSentTo is set when the email is changing: it changes only once the link sent to the
// new address is opened (see ConfirmEmailChangeEndpoint), so Profile still has the old one.
public record UpdateProfileResponse(MeResponse Profile, string? ConfirmationSentTo);

public static class UpdateProfileEndpoint
{
    [Authorize]
    [WolverinePut("/api/account/profile")]
    public static async Task<(Results<Ok<UpdateProfileResponse>, ValidationProblem, UnauthorizedHttpResult>, OutgoingMessages)> Put(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        HttpContext httpContext)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user is null)
        {
            return (TypedResults.Unauthorized(), []);
        }

        var messages = new OutgoingMessages();
        string? confirmationSentTo = null;
        if (!string.Equals(request.Email, user.Email, StringComparison.Ordinal))
        {
            var owner = await userManager.FindByEmailAsync(request.Email);
            if (owner is not null && owner.Id != user.Id)
            {
                var error = new IdentityErrorDescriber().DuplicateEmail(request.Email);
                return (IdentityResultExtensions.ValidationProblem(error.Code, error.Description), []);
            }

            messages.Add(new SendEmailChangeConfirmation(user.Id, request.Email, ClientUrl.For(configuration, httpContext)));
            confirmationSentTo = request.Email;
        }

        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return (result.ToValidationProblem(), []);
        }

        return (TypedResults.Ok(new UpdateProfileResponse(MeResponse.From(user), confirmationSentTo)), messages);
    }
}
