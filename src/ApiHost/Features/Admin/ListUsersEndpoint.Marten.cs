using ApiHost.Identity;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;

namespace ApiHost.Features.Admin;

public static class ListUsersEndpoint
{
    // Users ordered by email, optionally filtered by part of the email. Page numbers start at 1.
    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/users")]
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
            .Skip((pageNumber - 1) * AdminUsers.PageSize).Take(AdminUsers.PageSize)
            .ToListAsync(cancellationToken);

        // Marten keeps each user's role names on the user document.
        return new AdminUserList([.. users.Select(u => AdminUsers.ToAdminUser(u, [.. u.RoleClaims]))], total, pageNumber, AdminUsers.PageSize, AppRoles.All);
    }
}
