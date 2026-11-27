#if (Saas)
using ApiHost.Organisations;
#endif
#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using ApiHost.Data.EfCore;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public static class CreateTodoEndpoint
{
#if (Identity)
    [Authorize]
    [WolverinePost("/api/todos")]
    public static (TodoCreationResponse, OutgoingMessages) Post(CreateTodo command, AppDbContext db, ClaimsPrincipal user)
#else
    [WolverinePost("/api/todos")]
    public static (TodoCreationResponse, OutgoingMessages) Post(CreateTodo command, AppDbContext db)
#endif
    {
        var todo = new Todo
        {
            Id = Guid.CreateVersion7(),
#if (Identity)
            OwnerId = user.GetUserId(),
#endif
            Title = command.Title,
            IsComplete = false
        };

        // Saved when the endpoint returns (AutoApplyTransactions), together with the
        // notification in the outbox, so open tabs only hear about the Todo once it exists.
        db.Todos.Add(todo);

#if (Saas)
        // Wolverine stamps the new row with the request's organisation.
        return (new TodoCreationResponse(todo.Id), [TodoNotifications.Changed(todo.Id, user.GetOrganisationId().ToString())]);
#elif (Identity)
        return (new TodoCreationResponse(todo.Id), [TodoNotifications.Changed(todo.Id, todo.OwnerId)]);
#else
        return (new TodoCreationResponse(todo.Id), [TodoNotifications.Changed(todo.Id)]);
#endif
    }
}
