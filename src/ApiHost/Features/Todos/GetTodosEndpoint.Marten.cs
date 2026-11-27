#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using Marten;
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public static class GetTodosEndpoint
{
    // Reads the Todo documents that the inline snapshot projection keeps up to date.
#if (Identity)
    [Authorize]
    [WolverineGet("/api/todos")]
    public static Task<IReadOnlyList<Todo>> Get(IQuerySession session, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        return session.Query<Todo>().Where(t => t.OwnerId == userId).ToListAsync(cancellationToken);
    }
#else
    [WolverineGet("/api/todos")]
    public static Task<IReadOnlyList<Todo>> Get(IQuerySession session, CancellationToken cancellationToken)
    {
        return session.Query<Todo>().ToListAsync(cancellationToken);
    }
#endif
}
