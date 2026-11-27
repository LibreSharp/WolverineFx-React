using ApiHost.Identity.Email;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record RegisterRequest(string Email, string Password);

public static class RegisterEndpoint
{
    // Creates the account and emails a confirmation link; login is refused until it's opened
    // (SignIn.RequireConfirmedEmail, see IdentityJwtSetup).
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/register")]
    public static async Task<(Results<Created, ValidationProblem>, OutgoingMessages)> Post(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        HttpContext httpContext)
    {
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
