using Microsoft.AspNetCore.Identity;
#if (IsMarten)
using Marten.AspNetIdentity;
#endif

namespace ApiHost.Identity;

#if (IsMarten)
public class ApplicationUser : IdentityUser, IClaimsUser
{
    public IList<string> RoleClaims { get; set; } = new List<string>();
}
#else
public class ApplicationUser : IdentityUser
{
}
#endif
