using Microsoft.AspNetCore.DataProtection;

namespace ApiHost.Identity.External;

// Linking Google or GitHub from Settings means leaving the app for the provider in a full-page
// navigation, which can't carry the access token. So the signed-in client first asks for a
// ticket (POST /api/account/external-logins/link-ticket): the user's ID, encrypted and valid for
// five minutes, which it passes to /api/auth/external/{provider}?link=...
public class ExternalLinkTickets(IDataProtectionProvider dataProtection)
{
    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("ExternalLoginLink").ToTimeLimitedDataProtector();

    public string Create(string userId) => _protector.Protect(userId, TimeSpan.FromMinutes(5));

    public string? ReadUserId(string ticket)
    {
        try
        {
            return _protector.Unprotect(ticket);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null; // Tampered with, or expired.
        }
    }
}
