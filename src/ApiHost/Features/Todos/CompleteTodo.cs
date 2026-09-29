#if (IsMarten)
using Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
#endif
#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public record CompleteTodo(Guid Id);

public static class CompleteTodoEndpoint
{
#if (Identity)
    [Authorize]
#endif
    [WolverinePost("/api/todos/{id}/complete")]
#if (IsMarten && Identity)
    public static async Task<IResult> Post(Guid id, IDocumentSession session, ClaimsPrincipal user, CancellationToken cancellationToken)
#elif (IsMarten)
    public static async Task<IResult> Post(Guid id, IDocumentSession session, CancellationToken cancellationToken)
#elif (IsEfCore && Identity)
    public static async Task<IResult> Post(Guid id, AppDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
#elif (IsEfCore)
    public static async Task<IResult> Post(Guid id, AppDbContext db, CancellationToken cancellationToken)
#endif
    {
#if (IsMarten)
        var todo = await session.LoadAsync<Todo>(id, cancellationToken);
#elif (IsEfCore)
        var todo = await db.Todos.FindAsync([id], cancellationToken);
#endif
#if (Identity)
        // Another user's todo is reported as missing, so ids can't be probed for existence.
        if (todo is null || todo.OwnerId != user.GetUserId())
#else
        if (todo is null)
#endif
        {
            return Results.NotFound();
        }

        todo.IsComplete = true;
#if (IsMarten)
        session.Store(todo);
#endif

        return Results.NoContent();
    }
}
