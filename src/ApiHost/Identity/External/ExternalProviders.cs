using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity.External;

// Sign-in with Google and GitHub, each switched on by setting its client ID and secret:
//   Authentication:Google:ClientId / ClientSecret   (Google Cloud console, "OAuth client ID")
//   Authentication:GitHub:ClientId / ClientSecret   (GitHub, Settings > Developer settings > OAuth Apps)
// Register the callback URL with the provider: https://your-domain/api/auth/external/signin-google
// (or signin-github); in Development, http://localhost:5133/api/auth/external/signin-google.
//
// The provider's handler signs the person into Identity's short-lived external cookie, and
// ExternalLoginEndpoints turns that into this app's own tokens.
public static class ExternalProviders
{
    // Set to "true" when the provider says the email address is verified (see ExternalLoginService).
    public const string EmailVerifiedClaim = "email_verified";

    public static void AddExternalProviders(this AuthenticationBuilder authentication, IConfiguration configuration)
    {
        var google = configuration.GetSection("Authentication:Google");
        if (!string.IsNullOrEmpty(google["ClientId"]))
        {
            authentication.AddGoogle(options =>
            {
                options.ClientId = google["ClientId"]!;
                options.ClientSecret = google["ClientSecret"]!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.CallbackPath = "/api/auth/external/signin-google";
                options.ClaimActions.MapJsonKey(EmailVerifiedClaim, "email_verified");
            });
        }

        var gitHub = configuration.GetSection("Authentication:GitHub");
        if (!string.IsNullOrEmpty(gitHub["ClientId"]))
        {
            authentication.AddGitHub(options =>
            {
                options.ClientId = gitHub["ClientId"]!;
                options.ClientSecret = gitHub["ClientSecret"]!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.CallbackPath = "/api/auth/external/signin-github";
                options.Scope.Add("user:email");
                options.Events.OnCreatingTicket = UseVerifiedGitHubEmailAsync;
            });
        }
    }

    // GitHub's profile email can be missing (private) and its primary email may be unverified, so
    // ask for the account's email list and use the primary address only if GitHub has verified it.
    private static async Task UseVerifiedGitHubEmailAsync(Microsoft.AspNetCore.Authentication.OAuth.OAuthCreatingTicketContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ((GitHubAuthenticationOptions)context.Options).UserEmailsEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        using var emails = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
        var verified = emails.RootElement.EnumerateArray()
            .FirstOrDefault(e => e.TryGetProperty("primary", out var primary) && primary.GetBoolean()
                                 && e.TryGetProperty("verified", out var isVerified) && isVerified.GetBoolean());
        if (verified.ValueKind != JsonValueKind.Object || verified.GetProperty("email").GetString() is not { } email)
        {
            return;
        }

        var identity = context.Identity!;
        foreach (var claim in identity.FindAll(ClaimTypes.Email).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(ClaimTypes.Email, email));
        identity.AddClaim(new Claim(EmailVerifiedClaim, "true"));
    }
}
