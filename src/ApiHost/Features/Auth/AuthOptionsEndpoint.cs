using ApiHost.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wolverine.Http;

namespace ApiHost.Features.Auth;

public record ExternalProvider(string Name, string DisplayName);

// What the login and registration pages should offer: open registration, and the external
// sign-in providers that are configured (see External/ExternalProviders.cs).
public record AuthOptionsResponse(bool AllowRegistration, IReadOnlyList<ExternalProvider> ExternalProviders);

public static class AuthOptionsEndpoint
{
    [WolverineGet("/api/auth/options")]
    public static async Task<AuthOptionsResponse> Get(
        IOptionsSnapshot<AccountOptions> accountOptions, SignInManager<ApplicationUser> signInManager)
    {
        var schemes = await signInManager.GetExternalAuthenticationSchemesAsync();
        return new AuthOptionsResponse(
            accountOptions.Value.AllowRegistration,
            [.. schemes.Select(s => new ExternalProvider(s.Name, s.DisplayName ?? s.Name))]);
    }
}
