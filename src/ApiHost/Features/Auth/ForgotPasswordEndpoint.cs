using ApiHost.Identity;
using FluentValidation;
using ApiHost.Identity.Email;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Auth;

public record ForgotPasswordRequest(string Email);

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public static class ForgotPasswordEndpoint
{
    // Always answers 202, whether or not the email has an account, so the endpoint can't be
    // used to find out who is registered. If it does, emails a link to the client's
    // /reset-password page carrying a one-time token (valid for an hour; see TokenProviders).
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/forgot-password")]
    public static async Task<(Accepted, OutgoingMessages)> Post(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        HttpContext httpContext)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        var messages = new OutgoingMessages();
        if (user is not null)
        {
            messages.Add(new SendPasswordReset(user.Id, ClientUrl.For(configuration, httpContext)));
        }

        return (TypedResults.Accepted((string?)null), messages);
    }
}
