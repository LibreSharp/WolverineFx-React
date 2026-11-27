using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine.Http;

namespace ApiHost.Identity.External;

public record LinkedLogin(string Provider, string DisplayName);

public record LinkTicketResponse(string Ticket);

// The signed-in user's Google and GitHub logins, for Settings.
public static class LinkedLoginsEndpoints
{
    [Authorize]
    [WolverineGet("/api/account/external-logins")]
    public static async Task<Results<Ok<IReadOnlyList<LinkedLogin>>, UnauthorizedHttpResult>> List(
        ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var logins = await userManager.GetLoginsAsync(user);
        return TypedResults.Ok<IReadOnlyList<LinkedLogin>>(
            [.. logins.Select(l => new LinkedLogin(l.LoginProvider, l.ProviderDisplayName ?? l.LoginProvider))]);
    }

    // See ExternalLinkTickets.
    [Authorize]
    [WolverinePost("/api/account/external-logins/link-ticket")]
    public static LinkTicketResponse LinkTicket(ClaimsPrincipal principal, ExternalLinkTickets linkTickets)
        => new(linkTickets.Create(principal.GetUserId()));

    // Refused if it's the only way left to sign in (see SignInMethods).
    [Authorize]
    [WolverineDelete("/api/account/external-logins/{provider}")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem, UnauthorizedHttpResult>> Remove(
        string provider, ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var logins = await userManager.GetLoginsAsync(user);
        var login = logins.FirstOrDefault(l => string.Equals(l.LoginProvider, provider, StringComparison.OrdinalIgnoreCase));
        if (login is null)
        {
            return TypedResults.NotFound();
        }

        if (await userManager.CountSignInMethodsAsync(user) <= 1)
        {
            return IdentityResultExtensions.ValidationProblem("Provider",
                "This is the only way you can sign in. Add a passkey or set a password first (Forgot your password? on the login page).");
        }

        var result = await userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
        return result.Succeeded ? TypedResults.NoContent() : result.ToValidationProblem();
    }
}
