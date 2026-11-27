#if (Saas)
using ApiHost.Billing;
using ApiHost.Organisations;
using Microsoft.AspNetCore.Mvc;
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
#if (Saas)
    // Plan limits: a new Todo needs room in the organisation's OpenTodos limit, or the request gets
    // 402 Payment Required. Wolverine runs a Before method ahead of the endpoint, and stops there
    // when it returns a problem.
    public static Task<ProblemDetails> BeforeAsync(ClaimsPrincipal user, Entitlements entitlements, CancellationToken cancellationToken)
        => entitlements.RequireRoomAsync(user.GetOrganisationId(), PlanLimits.OpenTodos, 1, cancellationToken);

#endif
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
