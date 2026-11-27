using ApiHost.Identity;
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;

namespace ApiHost.Features.Admin;

public static class ListUsersEndpoint
{
    // Users ordered by email, optionally filtered by part of the email. Page numbers start at 1.
    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/users")]
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
            .Skip((pageNumber - 1) * AdminUsers.PageSize).Take(AdminUsers.PageSize)
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

        return new AdminUserList([.. users.Select(u => AdminUsers.ToAdminUser(u, [.. rolesByUser[u.Id]]))], total, pageNumber, AdminUsers.PageSize, AppRoles.All);
    }
}
