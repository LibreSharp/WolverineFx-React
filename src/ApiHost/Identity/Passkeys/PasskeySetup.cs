using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity.Passkeys;

// Passkeys use ASP.NET Core Identity's own WebAuthn support (IPasskeyHandler, added in .NET 10).
// Identity's SignInManager keeps a ceremony's state (its challenge) in a cookie; this API has no
// cookies, so PasskeyStates encrypts the state and the client sends it back with the result.
public static class PasskeySetup
{
    public static void AddPasskeys(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<PasskeyStates>();
        services.Configure<IdentityPasskeyOptions>(options =>
        {
            // The browser reports the page's origin. With the Vite dev server that's App:ClientUrl
            // (http://localhost:5173), not the API's own address, so accept both. The relying party
            // ID (ServerDomain) defaults to the request's host name, "localhost" for either.
            options.ValidateOrigin = context =>
            {
                if (context.CrossOrigin)
                {
                    return ValueTask.FromResult(false);
                }

                var request = context.HttpContext.Request;
                var allowed = new[] { $"{request.Scheme}://{request.Host}", configuration["App:ClientUrl"]?.TrimEnd('/') };
                return ValueTask.FromResult(allowed.Contains(context.Origin, StringComparer.OrdinalIgnoreCase));
            };
        });
    }
}

// Encrypts a passkey ceremony's state for the client to hold for up to five minutes. Registration
// state is bound to the user, so it can't be used to add a passkey to someone else's account.
public class PasskeyStates(IDataProtectionProvider dataProtection)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public string Protect(string state, string purpose, string? userId = null)
        => Protector(purpose, userId).Protect(state, Lifetime);

    public string? Unprotect(string protectedState, string purpose, string? userId = null)
    {
        try
        {
            return Protector(purpose, userId).Unprotect(protectedState);
        }
        catch (CryptographicException)
        {
            return null; // Tampered with, expired, or for another user.
        }
    }

    private ITimeLimitedDataProtector Protector(string purpose, string? userId)
        => dataProtection.CreateProtector("Passkey", purpose, userId ?? string.Empty).ToTimeLimitedDataProtector();
}
