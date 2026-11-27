#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

namespace ApiHost.Features.Todos;

public static class CompleteTodoEndpoint
{
    // Aggregate handler workflow: [WriteAggregate] loads the Todo by replaying its event
    // stream (the {id} route value is the stream id) and answers 404 if it doesn't exist.
    // The method is a pure function that decides what happens: Wolverine appends the
    // returned Events to the stream (with an optimistic concurrency check) and sends the
    // OutgoingMessages through the outbox, all in one transaction.
#if (Identity)
    [Authorize]
    [WolverinePost("/api/todos/{id}/complete")]
    public static (IResult, Events, OutgoingMessages) Post([WriteAggregate] Todo todo, ClaimsPrincipal user)
    {
        // Another user's todo is reported as missing, so ids can't be probed for existence.
        if (todo.OwnerId != user.GetUserId())
        {
            return (Results.NotFound(), [], []);
        }

#else
    [WolverinePost("/api/todos/{id}/complete")]
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
}
