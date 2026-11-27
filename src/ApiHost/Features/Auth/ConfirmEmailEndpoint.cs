using ApiHost.Identity;
using ApiHost.Identity.Email;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Auth;

public record ConfirmEmailRequest(string UserId, string Token);

public class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
    }
}

public record ResendConfirmationRequest(string Email);

public class ResendConfirmationRequestValidator : AbstractValidator<ResendConfirmationRequest>
{
    public ResendConfirmationRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public static class ConfirmEmailEndpoint
{
    // Opened from the link in the email sent on registration (the client's /confirm-email page).
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/confirm-email")]
    public static async Task<Results<NoContent, ValidationProblem>> Post(
        ConfirmEmailRequest request, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return InvalidLink();
        }

        // Opening the link again after it worked is fine.
        if (user.EmailConfirmed)
        {
            return TypedResults.NoContent();
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded ? TypedResults.NoContent() : InvalidLink();
    }

    // Always answers 202, so it can't be used to find out who is registered.
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/resend-confirmation")]
    public static async Task<(Accepted, OutgoingMessages)> Resend(
        ResendConfirmationRequest request,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        HttpContext httpContext)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        var messages = new OutgoingMessages();
        if (user is { EmailConfirmed: false })
        {
            messages.Add(new SendEmailConfirmation(user.Id, ClientUrl.For(configuration, httpContext)));
        }

        return (TypedResults.Accepted((string?)null), messages);
    }

    private static ValidationProblem InvalidLink()
        => IdentityResultExtensions.ValidationProblem("Token", "This confirmation link is invalid or has expired.");
}
