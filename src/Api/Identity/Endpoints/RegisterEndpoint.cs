using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine.Http;

namespace Api.Identity.Endpoints;

public record RegisterRequest(string Email, string Password);

public static class RegisterEndpoint
{
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
            return TypedResults.ValidationProblem(
                result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        }

        return TypedResults.Created();
    }
}
