#if (Saas)
using ApiHost.Organisations;
#endif
#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

namespace ApiHost.Features.Todos;

public static class CreateTodoEndpoint
{
    // Event sourcing: a new Todo is a new event stream that starts with TodoCreated.
    // The inline snapshot projection (MartenPersistence) writes the Todo document
    // that GET /api/todos reads, in the same transaction. The TodoChanged notification
    // goes through the outbox, so open tabs only hear about the Todo once it is saved.
#if (Saas)
    // The stream belongs to the request's organisation: Wolverine opens the session for it.
    [Authorize]
    [WolverinePost("/api/todos")]
    public static (TodoCreationResponse, IStartStream, OutgoingMessages) Post(CreateTodo command, ClaimsPrincipal user)
    {
        var id = Guid.CreateVersion7();
        return (
            new TodoCreationResponse(id),
            MartenOps.StartStream<Todo>(id, new TodoCreated(command.Title, user.GetUserId())),
            [TodoNotifications.Changed(id, user.GetOrganisationId().ToString())]);
    }
#elif (Identity)
    [Authorize]
    [WolverinePost("/api/todos")]
    public static (TodoCreationResponse, IStartStream, OutgoingMessages) Post(CreateTodo command, ClaimsPrincipal user)
    {
        var id = Guid.CreateVersion7();
        var ownerId = user.GetUserId();
        return (
            new TodoCreationResponse(id),
            MartenOps.StartStream<Todo>(id, new TodoCreated(command.Title, ownerId)),
            [TodoNotifications.Changed(id, ownerId)]);
    }
#else
    [WolverinePost("/api/todos")]
    public static (TodoCreationResponse, IStartStream, OutgoingMessages) Post(CreateTodo command)
    {
        var id = Guid.CreateVersion7();
        return (
            new TodoCreationResponse(id),
            MartenOps.StartStream<Todo>(id, new TodoCreated(command.Title)),
            [TodoNotifications.Changed(id)]);
    }
#endif
}
