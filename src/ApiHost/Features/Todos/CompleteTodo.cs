#if (IsMarten)
using Wolverine.Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
#endif
#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public static class CompleteTodoEndpoint
{
#if (Identity)
    [Authorize]
#endif
    [WolverinePost("/api/todos/{id}/complete")]
#if (IsMarten)
    // Aggregate handler workflow: [WriteAggregate] loads the Todo by replaying its event
    // stream (the {id} route value is the stream id) and answers 404 if it doesn't exist.
    // The method is a pure function that decides what happens: Wolverine appends the
    // returned Events to the stream (with an optimistic concurrency check) and sends the
    // OutgoingMessages through the outbox, all in one transaction.
#if (Identity)
    public static (IResult, Events, OutgoingMessages) Post([WriteAggregate] Todo todo, ClaimsPrincipal user)
    {
        // Another user's todo is reported as missing, so ids can't be probed for existence.
        if (todo.OwnerId != user.GetUserId())
        {
            return (Results.NotFound(), [], []);
        }

#else
    public static (IResult, Events, OutgoingMessages) Post([WriteAggregate] Todo todo)
    {
#endif
        // Completing twice is a no-op, so the event (and the message) only happen once.
        if (todo.IsComplete)
        {
            return (Results.NoContent(), [], []);
        }

#if (Identity)
        var completed = new TodoCompleted(todo.Id, todo.Title, todo.OwnerId);
#else
        var completed = new TodoCompleted(todo.Id, todo.Title);
#endif
        return (Results.NoContent(), [completed], [completed]);
    }
#elif (IsEfCore)
#if (Identity)
    public static async Task<(IResult, OutgoingMessages)> Post(Guid id, AppDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
#else
    public static async Task<(IResult, OutgoingMessages)> Post(Guid id, AppDbContext db, CancellationToken cancellationToken)
#endif
    {
        var todo = await db.Todos.FindAsync([id], cancellationToken);
#if (Identity)
        // Another user's todo is reported as missing, so ids can't be probed for existence.
        if (todo is null || todo.OwnerId != user.GetUserId())
#else
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
#endif
}
