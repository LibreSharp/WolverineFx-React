using Marten;
using Marten.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace ApiHost.Identity.Marten;

// Marten.AspNetIdentity's user store, plus the parts of ASP.NET Core Identity it doesn't
// implement. Each interface added here switches on an Identity feature (UserManager checks
// SupportsUserSecurityStamp and so on); the data lives on the ApplicationUser document, and
// UserManager saves it by calling UpdateAsync after changing it.
//
// - IUserSecurityStampStore: one-time tokens (email confirmation, password reset) stop working
//   once used, because using one changes the stamp.
// - IUserLockoutStore: account lockout after repeated failed logins, and locking by an admin.
// - IUserRoleStore: UserManager's role methods (AddToRoleAsync, GetRolesAsync, ...), keeping the
//   role names in the user document's RoleClaims, where Marten.AspNetIdentity puts them.
// - IUserLoginStore: external sign-in (Google, GitHub), kept in the user document's Logins.
// - IUserPasskeyStore: passkeys, kept in the user document's Passkeys.
//
// Marten.AspNetIdentity has no nullable annotations, so its FindBy...Async methods don't match
// Identity's (which may return null); the warning is about annotations only.
#pragma warning disable CS8613
public class ApplicationUserStore
    : MartenUserStore<ApplicationUser>,
      IUserSecurityStampStore<ApplicationUser>,
      IUserLockoutStore<ApplicationUser>,
      IUserRoleStore<ApplicationUser>,
      IUserLoginStore<ApplicationUser>,
      IUserPasskeyStore<ApplicationUser>
{
    // The base class keeps its own reference privately.
    private readonly IDocumentStore _documentStore;

    public ApplicationUserStore(IDocumentStore documentStore, ILogger<MartenUserStore<ApplicationUser>> logger)
        : base(documentStore, logger)
    {
        _documentStore = documentStore;
    }

    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    // UserManager passes role names normalized (upper case); the document keeps the role's name.
    public async Task AddToRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken)
    {
        var roleName = await FindRoleNameAsync(normalizedRoleName, cancellationToken)
            ?? throw new InvalidOperationException($"Role {normalizedRoleName} does not exist (see RoleSeeder)");
        if (!user.RoleClaims.Contains(roleName, StringComparer.OrdinalIgnoreCase))
        {
            user.RoleClaims.Add(roleName);
        }
    }

    public Task RemoveFromRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken)
    {
        foreach (var role in user.RoleClaims.Where(r => string.Equals(r, normalizedRoleName, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            user.RoleClaims.Remove(role);
        }

        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult<IList<string>>(user.RoleClaims.ToList());

    public Task<bool> IsInRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken)
        => Task.FromResult(user.RoleClaims.Contains(normalizedRoleName, StringComparer.OrdinalIgnoreCase));

    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        var roleName = await FindRoleNameAsync(normalizedRoleName, cancellationToken);
        if (roleName is null)
        {
            return [];
        }

        await using var session = _documentStore.QuerySession();
        return (await session.Query<ApplicationUser>().Where(u => u.RoleClaims.Contains(roleName)).ToListAsync(cancellationToken)).ToList();
    }

    public Task AddLoginAsync(ApplicationUser user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        user.Logins.Add(new StoredLogin(login.LoginProvider, login.ProviderKey, login.ProviderDisplayName));
        return Task.CompletedTask;
    }

    public Task RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        foreach (var login in user.Logins.Where(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey).ToList())
        {
            user.Logins.Remove(login);
        }

        return Task.CompletedTask;
    }

    public Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult<IList<UserLoginInfo>>(
            [.. user.Logins.Select(l => new UserLoginInfo(l.LoginProvider, l.ProviderKey, l.ProviderDisplayName))]);

    public async Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        await using var session = _documentStore.QuerySession();
        return await session.Query<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Logins.Any(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey), cancellationToken);
    }

    public Task AddOrUpdatePasskeyAsync(ApplicationUser user, UserPasskeyInfo passkey, CancellationToken cancellationToken)
    {
        var id = WebEncoders.Base64UrlEncode(passkey.CredentialId);
        var stored = new StoredPasskey(id, passkey.PublicKey, passkey.Name, passkey.CreatedAt, passkey.SignCount, passkey.Transports,
            passkey.IsUserVerified, passkey.IsBackupEligible, passkey.IsBackedUp, passkey.AttestationObject, passkey.ClientDataJson, passkey.Aaguid);
        var index = user.Passkeys.ToList().FindIndex(p => p.CredentialId == id);
        if (index >= 0)
        {
            user.Passkeys[index] = stored;
        }
        else
        {
            user.Passkeys.Add(stored);
        }

        return Task.CompletedTask;
    }

    public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult<IList<UserPasskeyInfo>>([.. user.Passkeys.Select(ToPasskeyInfo)]);

    public async Task<ApplicationUser?> FindByPasskeyIdAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        var id = WebEncoders.Base64UrlEncode(credentialId);
        await using var session = _documentStore.QuerySession();
        return await session.Query<ApplicationUser>().FirstOrDefaultAsync(u => u.Passkeys.Any(p => p.CredentialId == id), cancellationToken);
    }

    public Task<UserPasskeyInfo?> FindPasskeyAsync(ApplicationUser user, byte[] credentialId, CancellationToken cancellationToken)
    {
        var id = WebEncoders.Base64UrlEncode(credentialId);
        var stored = user.Passkeys.FirstOrDefault(p => p.CredentialId == id);
        return Task.FromResult(stored is null ? null : ToPasskeyInfo(stored));
    }

    public Task RemovePasskeyAsync(ApplicationUser user, byte[] credentialId, CancellationToken cancellationToken)
    {
        var id = WebEncoders.Base64UrlEncode(credentialId);
        foreach (var passkey in user.Passkeys.Where(p => p.CredentialId == id).ToList())
        {
            user.Passkeys.Remove(passkey);
        }

        return Task.CompletedTask;
    }

    private static UserPasskeyInfo ToPasskeyInfo(StoredPasskey p)
        => new(WebEncoders.Base64UrlDecode(p.CredentialId), p.PublicKey, p.CreatedAt, p.SignCount, p.Transports ?? [],
            p.IsUserVerified, p.IsBackupEligible, p.IsBackedUp, p.AttestationObject, p.ClientDataJson)
        {
            Name = p.Name,
            Aaguid = p.Aaguid,
        };

    private async Task<string?> FindRoleNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        await using var session = _documentStore.QuerySession();
        var role = await session.Query<IdentityRole>().FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);
        return role?.Name;
    }
}
#pragma warning restore CS8613
