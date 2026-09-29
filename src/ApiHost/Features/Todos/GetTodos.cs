#if (IsMarten)
using Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
#endif
#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public static class GetTodosEndpoint
{
#if (Identity)
    [Authorize]
#endif
    [WolverineGet("/api/todos")]
#if (IsMarten && Identity)
    public static Task<IReadOnlyList<Todo>> Get(IQuerySession session, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        return session.Query<Todo>().Where(t => t.OwnerId == userId).ToListAsync(cancellationToken);
    }
#elif (IsMarten)
    public static Task<IReadOnlyList<Todo>> Get(IQuerySession session, CancellationToken cancellationToken)
    {
        return session.Query<Todo>().ToListAsync(cancellationToken);
    }
#elif (IsEfCore && Identity)
    public static Task<List<Todo>> Get(AppDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        return db.Todos.AsNoTracking().Where(t => t.OwnerId == userId).ToListAsync(cancellationToken);
    }
#elif (IsEfCore)
    public static Task<List<Todo>> Get(AppDbContext db, CancellationToken cancellationToken)
    {
        return db.Todos.AsNoTracking().ToListAsync(cancellationToken);
    }
#endif
}
