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
}

public record StoredLogin(string LoginProvider, string ProviderKey, string? ProviderDisplayName);
#else
public class ApplicationUser : IdentityUser
{
    // Optional name shown in the app instead of the email address.
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? DisplayName { get; set; }
}
#endif
