#if (IsMarten)
using Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
#endif
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public static class GetTodosEndpoint
{
    [WolverineGet("/api/todos")]
#if (IsMarten)
    public static Task<IReadOnlyList<Todo>> Get(IQuerySession session, CancellationToken cancellationToken)
    {
        return session.Query<Todo>().ToListAsync(cancellationToken);
    }
#elif (IsEfCore)
    public static Task<List<Todo>> Get(AppDbContext db, CancellationToken cancellationToken)
    {
        return db.Todos.AsNoTracking().ToListAsync(cancellationToken);
    }
#endif
}
