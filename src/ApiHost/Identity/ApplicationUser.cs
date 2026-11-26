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
}
#else
public class ApplicationUser : IdentityUser
{
    // Optional name shown in the app instead of the email address.
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? DisplayName { get; set; }
}
#endif
