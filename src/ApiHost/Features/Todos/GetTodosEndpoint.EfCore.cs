#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public static class GetTodosEndpoint
{
#if (Identity)
    [Authorize]
    [WolverineGet("/api/todos")]
    public static async Task<IReadOnlyList<Todo>> Get(AppDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        return await db.Todos.AsNoTracking().Where(t => t.OwnerId == userId).ToListAsync(cancellationToken);
    }
#else
    [WolverineGet("/api/todos")]
    public static async Task<IReadOnlyList<Todo>> Get(AppDbContext db, CancellationToken cancellationToken)
    {
        return await db.Todos.AsNoTracking().ToListAsync(cancellationToken);
    }
#endif
}
