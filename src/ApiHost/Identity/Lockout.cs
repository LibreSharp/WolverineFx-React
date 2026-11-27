using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity;

// Account lockout is ASP.NET Core Identity's: SignInManager counts failed logins and locks the
// account for Lockout.DefaultLockoutTimeSpan after Lockout.MaxFailedAccessAttempts (set in
// IdentityJwtSetup). An admin lock is the same lockout with no end, Identity's convention for
// "locked until unlocked".
public static class Lockout
{
    public static readonly DateTimeOffset Indefinitely = DateTimeOffset.MaxValue;

    public static bool IsLockedByAdmin(this ApplicationUser user) => user.LockoutEnd == Indefinitely;

    // After a password reset: the user proved they own the email, so lift a lockout from failed
    // logins (but not an admin's lock) and start counting failures again.
    public static async Task ClearFailedLoginLockoutAsync(this UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        if (!user.IsLockedByAdmin())
        {
            await userManager.SetLockoutEndDateAsync(user, null);
        }

        await userManager.ResetAccessFailedCountAsync(user);
    }
}
