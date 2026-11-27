#if (Saas)
using JasperFx.MultiTenancy;
#endif
#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public static class RemindTodoEndpoint
{
    // Schedules a TodoReminderDue message. Local queues are durable (Program.cs), so scheduled
    // messages are stored in PostgreSQL and still fire after a restart.
#if (Saas)
    [Authorize]
    [WolverinePost("/api/todos/{id}/remind")]
    public static async Task<(Results<Accepted<TodoReminderScheduled>, NotFound>, OutgoingMessages)> Post(
        Guid id, RemindTodo request, IQuerySession session, CancellationToken cancellationToken)
    {
        // Only the request's organisation's Todos are found.
        var todo = await session.LoadAsync<Todo>(id, cancellationToken);
        if (todo is null)
#elif (Identity)
    [Authorize]
    [WolverinePost("/api/todos/{id}/remind")]
    public static async Task<(Results<Accepted<TodoReminderScheduled>, NotFound>, OutgoingMessages)> Post(
        Guid id, RemindTodo request, IQuerySession session, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var todo = await session.LoadAsync<Todo>(id, cancellationToken);
        if (todo is null || todo.OwnerId != user.GetUserId())
#else
    [WolverinePost("/api/todos/{id}/remind")]
    public static async Task<(Results<Accepted<TodoReminderScheduled>, NotFound>, OutgoingMessages)> Post(
        Guid id, RemindTodo request, IQuerySession session, CancellationToken cancellationToken)
    {
        var todo = await session.LoadAsync<Todo>(id, cancellationToken);
        if (todo is null)
#endif
        {
            return (TypedResults.NotFound(), []);
        }

        var delay = TimeSpan.FromMinutes(request.Minutes);
        var messages = new OutgoingMessages();
        messages.Delay(new TodoReminderDue(todo.Id), delay);
        return (TypedResults.Accepted((string?)null, new TodoReminderScheduled(DateTimeOffset.UtcNow.Add(delay))), messages);
    }
}

public static class TodoReminderDueHandler
{
    // Runs when the reminder is due. Nothing happens if the Todo was completed or deleted in
    // the meantime; otherwise the owner's open tabs show the reminder.
#if (Saas)
    // tenantId: the organisation the reminder was scheduled in; Wolverine keeps it on the message.
    public static async Task<object?> Handle(TodoReminderDue message, IQuerySession session, TenantId tenantId, CancellationToken cancellationToken)
#else
    public static async Task<object?> Handle(TodoReminderDue message, IQuerySession session, CancellationToken cancellationToken)
#endif
    {
        var todo = await session.LoadAsync<Todo>(message.TodoId, cancellationToken);
        if (todo is null || todo.IsComplete)
        {
            return null;
        }

#if (Saas)
        return TodoNotifications.Reminder(todo, tenantId.Value);
#else
        return TodoNotifications.Reminder(todo);
#endif
    }
}
