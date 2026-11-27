using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity;

public static class SignInMethods
{
    // How many ways the user has to sign in: a password, linked Google/GitHub logins, passkeys.
    // Removing the last one would lock them out, so Settings refuses to.
    public static async Task<int> CountSignInMethodsAsync(this UserManager<ApplicationUser> userManager, ApplicationUser user)
        => (await userManager.HasPasswordAsync(user) ? 1 : 0)
           + (await userManager.GetLoginsAsync(user)).Count
           + (await userManager.GetPasskeysAsync(user)).Count;
}
