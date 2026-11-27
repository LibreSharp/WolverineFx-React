using Marten.AspNetIdentity;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity;

// A user document. Marten.AspNetIdentity stores Identity's users as documents, and
// ApplicationUserStore (Identity/Marten) keeps roles, external logins and passkeys on it.
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
