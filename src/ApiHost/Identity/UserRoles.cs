using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity;

public static class UserRoles
{
    // Use this instead of UserManager.GetRolesAsync, which only works with an IUserRoleStore.
#if (IsMarten)
    // Marten.AspNetIdentity keeps roles on the user document (IClaimsUser.RoleClaims) and its
    // user store does not implement IUserRoleStore, so GetRolesAsync would throw.
    public static Task<IList<string>> GetUserRolesAsync(this UserManager<ApplicationUser> userManager, ApplicationUser user)
        => Task.FromResult(user.RoleClaims);
#else
    public static Task<IList<string>> GetUserRolesAsync(this UserManager<ApplicationUser> userManager, ApplicationUser user)
        => userManager.GetRolesAsync(user);
#endif
}
