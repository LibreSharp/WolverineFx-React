using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Identity.External;

// Sign-in with Google or GitHub for this single-page app:
// 1. The browser navigates (a full page load, not fetch) to /api/auth/external/{provider}, which
//    redirects to the provider.
// 2. The provider sends the browser back to the handler's callback (.../signin-google), which
//    signs into Identity's short-lived external cookie and redirects to /api/auth/external/callback.
// 3. The callback signs in or links the account (ExternalLoginService), clears the external
//    cookie, and redirects to the client's /external-login page with a refresh token in the URL
//    fragment, which browsers never send to a server. The client swaps it straight away at
//    /api/auth/refresh, so the token in the URL is used once (refresh tokens rotate, and reuse is
//    detected).
public static class ExternalLoginEndpoints
{
    public const string CallbackPath = "/api/auth/external/callback";

    // link: a ticket from the link-ticket endpoint, to link the provider to that signed-in account.
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverineGet("/api/auth/external/{provider}")]
    public static async Task<Results<ChallengeHttpResult, NotFound, RedirectHttpResult>> Start(
        string provider,
        string? link,
        SignInManager<ApplicationUser> signInManager,
        ExternalLinkTickets linkTickets,
        IConfiguration configuration,
        HttpContext httpContext)
    {
        var scheme = (await signInManager.GetExternalAuthenticationSchemesAsync())
            .FirstOrDefault(s => string.Equals(s.Name, provider, StringComparison.OrdinalIgnoreCase));
        if (scheme is null)
        {
            return TypedResults.NotFound();
        }

        string? linkUserId = null;
        if (link is not null)
        {
            linkUserId = linkTickets.ReadUserId(link);
            if (linkUserId is null)
            {
                return ToClient(configuration, httpContext, "settings", ("error", "That link request expired. Please try again."));
            }
        }

        // The user ID (when linking) travels inside the protected external cookie, and Identity
        // checks it again in the callback.
        var properties = signInManager.ConfigureExternalAuthenticationProperties(scheme.Name, CallbackPath, linkUserId);
        return TypedResults.Challenge(properties, [scheme.Name]);
    }

    [WolverineGet(CallbackPath)]
    public static async Task<RedirectHttpResult> Callback(
        HttpContext httpContext,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ExternalLoginService externalLogins,
        TokenIssuer tokenIssuer,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        // Set by Start when linking to a signed-in account (Identity's "XsrfId" item).
        var external = await httpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        string? linkUserId = null;
        external.Properties?.Items.TryGetValue("XsrfId", out linkUserId);

        var info = await signInManager.GetExternalLoginInfoAsync(linkUserId);
        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        if (info is null)
        {
            return ToClient(configuration, httpContext, linkUserId is null ? "external-login" : "settings",
                ("error", "Signing in with that provider didn't work. Please try again."));
        }

        if (linkUserId is not null)
        {
            var user = await userManager.FindByIdAsync(linkUserId);
            var linkError = user is null ? "Your account no longer exists." : await externalLogins.LinkAsync(user, info);
            return linkError is null
                ? ToClient(configuration, httpContext, "settings", ("linked", info.ProviderDisplayName ?? info.LoginProvider))
                : ToClient(configuration, httpContext, "settings", ("error", linkError));
        }

        var result = await externalLogins.SignInAsync(info, cancellationToken);
        if (result.User is null)
        {
            return ToClient(configuration, httpContext, "external-login", ("error", result.Error!));
        }

        var tokens = await tokenIssuer.IssueAsync(result.User, cancellationToken);
        return ToClient(configuration, httpContext, "external-login", ("refreshToken", tokens.RefreshToken));
    }

    // A client page with values in the URL fragment (#name=value).
    private static RedirectHttpResult ToClient(IConfiguration configuration, HttpContext httpContext, string page, (string Name, string Value) value)
        => TypedResults.Redirect($"{ClientUrl.For(configuration, httpContext)}/{page}#{value.Name}={Uri.EscapeDataString(value.Value)}");
}
