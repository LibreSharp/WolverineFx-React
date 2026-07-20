#if (IsMarten)
using Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
#endif
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public record CompleteTodo(Guid Id);

public static class CompleteTodoEndpoint
{
    [WolverinePost("/api/todos/{id}/complete")]
#if (IsMarten)
    public static async Task<IResult> Post(Guid id, IDocumentSession session, CancellationToken cancellationToken)
    {
        var todo = await session.LoadAsync<Todo>(id, cancellationToken);
        if (todo is null)
        {
            return Results.NotFound();
        }

        todo.IsComplete = true;
        session.Store(todo);

        return Results.NoContent();
    }
#elif (IsEfCore)
    public static async Task<IResult> Post(Guid id, AppDbContext db, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.FindAsync([id], cancellationToken);
        if (todo is null)
        {
            return Results.NotFound();
        }

        todo.IsComplete = true;

        return Results.NoContent();
    }
#endif
}
