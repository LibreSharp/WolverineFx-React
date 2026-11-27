using ApiHost.Identity;
using ApiHost.Identity.Email;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Auth;

public record RegisterRequest(string Email, string Password);

public static class RegisterEndpoint
{
    // Creates the account and emails a confirmation link; login is refused until it's opened
    // (SignIn.RequireConfirmedEmail, see IdentityJwtSetup). With Accounts:AllowRegistration set to
    // false, only invited people get accounts, and this answers 403.
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/register")]
    public static async Task<(Results<Created, ValidationProblem, ProblemHttpResult>, OutgoingMessages)> Post(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        IOptionsSnapshot<AccountOptions> accountOptions,
        IConfiguration configuration,
        HttpContext httpContext)
    {
        if (!accountOptions.Value.AllowRegistration)
        {
            return (TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Registration is closed: accounts are by invitation only."), []);
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return (result.ToValidationProblem(), []);
        }

        return (TypedResults.Created(), [new SendEmailConfirmation(user.Id, ClientUrl.For(configuration, httpContext))]);
    }
}
