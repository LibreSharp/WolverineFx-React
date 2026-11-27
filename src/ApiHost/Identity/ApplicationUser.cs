using Microsoft.AspNetCore.Identity;
#if (IsMarten)
using Marten.AspNetIdentity;
#endif

namespace ApiHost.Identity;

#if (IsMarten)
public class ApplicationUser : IdentityUser, IClaimsUser
{
    public IList<string> RoleClaims { get; set; } = new List<string>();

    // Optional name shown in the app instead of the email address.
    public string? DisplayName { get; set; }

    // External sign-ins (Google, GitHub) linked to this account; see ApplicationUserStore.
    public IList<StoredLogin> Logins { get; set; } = new List<StoredLogin>();

    // Passkeys registered for this account; see ApplicationUserStore.
    public IList<StoredPasskey> Passkeys { get; set; } = new List<StoredPasskey>();
}

public record StoredLogin(string LoginProvider, string ProviderKey, string? ProviderDisplayName);

// Identity's UserPasskeyInfo as stored in the user document. CredentialId is base64url text, so
// FindByPasskeyIdAsync can query it.
public record StoredPasskey(
    string CredentialId,
    byte[] PublicKey,
    string? Name,
    DateTimeOffset CreatedAt,
    uint SignCount,
    string[]? Transports,
    bool IsUserVerified,
    bool IsBackupEligible,
    bool IsBackedUp,
    byte[] AttestationObject,
    byte[] ClientDataJson,
    byte[]? Aaguid);
#else
public class ApplicationUser : IdentityUser
{
    // Optional name shown in the app instead of the email address.
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? DisplayName { get; set; }
}
#endif
