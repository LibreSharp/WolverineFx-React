#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using ApiHost.Data.EfCore;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public static class CompleteTodoEndpoint
{
#if (Identity)
    [Authorize]
    [WolverinePost("/api/todos/{id}/complete")]
    public static async Task<(IResult, OutgoingMessages)> Post(Guid id, AppDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.FindAsync([id], cancellationToken);
        // Another user's todo is reported as missing, so ids can't be probed for existence.
        if (todo is null || todo.OwnerId != user.GetUserId())
#else
    [WolverinePost("/api/todos/{id}/complete")]
    public static async Task<(IResult, OutgoingMessages)> Post(Guid id, AppDbContext db, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.FindAsync([id], cancellationToken);
        if (todo is null)
#endif
        {
            return (Results.NotFound(), []);
        }

        // Completing twice is a no-op, so the message is only sent once.
        if (todo.IsComplete)
        {
            return (Results.NoContent(), []);
        }

        todo.IsComplete = true;

        // Cascaded message: Wolverine saves it to the outbox in the same transaction as
        // the change above (AutoApplyTransactions), then TodoCompletedHandler runs it.
#if (Identity)
        return (Results.NoContent(), [new TodoCompleted(todo.Id, todo.Title, todo.OwnerId)]);
#else
        return (Results.NoContent(), [new TodoCompleted(todo.Id, todo.Title)]);
#endif
    }
}
