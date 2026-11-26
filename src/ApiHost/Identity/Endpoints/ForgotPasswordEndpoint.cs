using FluentValidation;
using ApiHost.Identity.Email;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

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
    // /reset-password page carrying a one-time token (valid for an hour; see IdentityJwtSetup).
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/forgot-password")]
    public static async Task<Accepted> Post(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IAccountEmailSender emailSender,
        IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user?.Email is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = QueryHelpers.AddQueryString($"{ClientUrl(configuration, httpContext)}/reset-password",
                new Dictionary<string, string?> { ["email"] = user.Email, ["token"] = token });
            await emailSender.SendPasswordResetAsync(user.Email, link, cancellationToken);
        }

        return TypedResults.Accepted((string?)null);
    }

    // Where the React app is served: App:ClientUrl if set (Development, where the Vite dev
    // server runs on another port), otherwise this API's own address (production, where the
    // API serves the app).
    private static string ClientUrl(IConfiguration configuration, HttpContext httpContext)
        => configuration["App:ClientUrl"]?.TrimEnd('/')
           ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";
}
