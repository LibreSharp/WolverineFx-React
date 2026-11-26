using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

public record RegisterRequest(string Email, string Password);

public static class RegisterEndpoint
{
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/auth/register")]
    public static async Task<Results<Created, ValidationProblem>> Post(
        RegisterRequest request, UserManager<ApplicationUser> userManager)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return result.ToValidationProblem();
        }

        return TypedResults.Created();
    }
}
