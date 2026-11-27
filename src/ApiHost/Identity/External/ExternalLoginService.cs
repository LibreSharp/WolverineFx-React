using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ApiHost.Identity.External;

public record ExternalSignInResult(ApplicationUser? User, string? Error)
{
    public static ExternalSignInResult Fail(string error) => new(null, error);
}

// What happens when someone comes back from Google or GitHub, through UserManager:
// - a login already linked to an account signs into that account;
// - otherwise the provider must vouch for the email address (verified). An account with that
//   email gets the login linked; with no account, one is created (unless registration is closed).
// - locked-out accounts are refused, as with a password login.
public class ExternalLoginService(
    UserManager<ApplicationUser> userManager,
    IRefreshTokenStore refreshTokenStore,
    IOptionsSnapshot<AccountOptions> accountOptions)
{
    public async Task<ExternalSignInResult> SignInAsync(ExternalLoginInfo info, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (user is null)
        {
            var byEmail = await FindOrCreateByVerifiedEmailAsync(info, cancellationToken);
            if (byEmail.User is null)
            {
                return byEmail;
            }

            user = byEmail.User;
            var added = await userManager.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
            if (!added.Succeeded)
            {
                return ExternalSignInResult.Fail(string.Join(" ", added.Errors.Select(e => e.Description)));
            }
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return ExternalSignInResult.Fail("This account is locked.");
        }

        return new ExternalSignInResult(user, null);
    }

    private async Task<ExternalSignInResult> FindOrCreateByVerifiedEmailAsync(ExternalLoginInfo info, CancellationToken cancellationToken)
    {
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (email is null || !IsEmailVerified(info))
        {
            return ExternalSignInResult.Fail(
                $"{info.ProviderDisplayName ?? info.LoginProvider} didn't share a verified email address, so we can't sign you in with it.");
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            if (!accountOptions.Value.AllowRegistration)
            {
                return ExternalSignInResult.Fail("Registration is closed: accounts are by invitation only.");
            }

            // No password: this account signs in with the provider (a password can be set later
            // through "Forgot your password?").
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name),
            };
            var created = await userManager.CreateAsync(user);
            return created.Succeeded
                ? new ExternalSignInResult(user, null)
                : ExternalSignInResult.Fail(string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        if (!user.EmailConfirmed)
        {
            // Someone registered this email but never proved they own it, and the provider says
            // this person does. Remove that unproven password, so whoever registered can't use it
            // to get into the account once it's confirmed (account pre-hijacking).
            if (await userManager.HasPasswordAsync(user))
            {
                await userManager.RemovePasswordAsync(user);
            }

            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
            await refreshTokenStore.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        return new ExternalSignInResult(user, null);
    }

    // Links the login to a signed-in user's account (from Settings). Returns an error, or null.
    public async Task<string?> LinkAsync(ApplicationUser user, ExternalLoginInfo info)
    {
        var provider = info.ProviderDisplayName ?? info.LoginProvider;
        var owner = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (owner is not null)
        {
            return owner.Id == user.Id ? null : $"This {provider} account is already linked to another account.";
        }

        var result = await userManager.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
        return result.Succeeded ? null : string.Join(" ", result.Errors.Select(e => e.Description));
    }

    private static bool IsEmailVerified(ExternalLoginInfo info)
        => string.Equals(info.Principal.FindFirstValue(ExternalProviders.EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase);
}
