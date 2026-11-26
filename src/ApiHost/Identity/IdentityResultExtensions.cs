using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity;

public static class IdentityResultExtensions
{
    // ASP.NET Core Identity reports failures (weak password, email taken, wrong current
    // password...) as IdentityErrors; return them as a 400 ValidationProblem, the same shape
    // FluentValidation failures use, so the client shows both the same way.
    public static ValidationProblem ToValidationProblem(this IdentityResult result)
        => TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

    public static ValidationProblem ValidationProblem(string field, string message)
        => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
