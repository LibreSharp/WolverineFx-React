using Marten;
using Marten.AspNetIdentity;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity.Marten;

// Marten.AspNetIdentity's user store, plus the parts of ASP.NET Core Identity it doesn't
// implement. Each interface added here switches on an Identity feature (UserManager checks
// SupportsUserSecurityStamp and so on); the data lives on the ApplicationUser document, and
// UserManager saves it by calling UpdateAsync after changing it.
//
// - IUserSecurityStampStore: one-time tokens (email confirmation, password reset) stop working
//   once used, because using one changes the stamp.
// - IUserLockoutStore: account lockout after repeated failed logins, and locking by an admin.
//
// Marten.AspNetIdentity has no nullable annotations, so its FindBy...Async methods don't match
// Identity's (which may return null); the warning is about annotations only.
#pragma warning disable CS8613
public class ApplicationUserStore(IDocumentStore documentStore, ILogger<MartenUserStore<ApplicationUser>> logger)
    : MartenUserStore<ApplicationUser>(documentStore, logger),
      IUserSecurityStampStore<ApplicationUser>,
      IUserLockoutStore<ApplicationUser>
{
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
}
#pragma warning restore CS8613
