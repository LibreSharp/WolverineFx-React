#if (IsMarten)
using Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
#endif
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;

namespace ApiHost.Identity.Admin;

// InvitationPending: invited by an admin, and hasn't chosen a password yet.
// LockedUntil: set while the account is locked out; LockedByAdmin when an admin locked it.
public record AdminUser(
    string Id,
    string Email,
    string? DisplayName,
    bool EmailConfirmed,
    bool InvitationPending,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LockedUntil,
    bool LockedByAdmin);

// AllRoles: the roles an admin can assign (AppRoles.All).
public record AdminUserList(IReadOnlyList<AdminUser> Users, int Total, int Page, int PageSize, IReadOnlyList<string> AllRoles);

public static class ListUsersEndpoint
{
    public const int PageSize = 25;

    // Users ordered by email, optionally filtered by part of the email. Page numbers start at 1.
    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/users")]
#if (IsMarten)
    public static async Task<AdminUserList> Get(string? search, int? page, IQuerySession session, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(page ?? 1, 1);
        var query = session.Query<ApplicationUser>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToUpperInvariant();
            query = query.Where(u => u.NormalizedEmail!.Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(u => u.NormalizedEmail)
            .Skip((pageNumber - 1) * PageSize).Take(PageSize)
            .ToListAsync(cancellationToken);

        // Marten keeps each user's role names on the user document.
        return new AdminUserList([.. users.Select(u => ToAdminUser(u, [.. u.RoleClaims]))], total, pageNumber, PageSize, AppRoles.All);
    }
#elif (IsEfCore)
    public static async Task<AdminUserList> Get(string? search, int? page, AppDbContext db, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(page ?? 1, 1);
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToUpperInvariant();
            query = query.Where(u => u.NormalizedEmail!.Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(u => u.NormalizedEmail)
            .Skip((pageNumber - 1) * PageSize).Take(PageSize)
            .ToListAsync(cancellationToken);

        // The page's role names in one query, rather than one per user.
        var ids = users.Select(u => u.Id).ToList();
        var roles = await (
                from userRole in db.UserRoles
                join role in db.Roles on userRole.RoleId equals role.Id
                where ids.Contains(userRole.UserId)
                select new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);
        var rolesByUser = roles.ToLookup(r => r.UserId, r => r.Name!, StringComparer.Ordinal);

        return new AdminUserList([.. users.Select(u => ToAdminUser(u, [.. rolesByUser[u.Id]]))], total, pageNumber, PageSize, AppRoles.All);
    }
#endif

    private static AdminUser ToAdminUser(ApplicationUser user, IReadOnlyList<string> roles) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.DisplayName,
        user.EmailConfirmed,
        InvitationPending: user.PasswordHash is null && !user.EmailConfirmed,
        roles,
        LockedUntil: user.LockoutEnd > DateTimeOffset.UtcNow ? user.LockoutEnd : null,
        LockedByAdmin: user.IsLockedByAdmin());
}
