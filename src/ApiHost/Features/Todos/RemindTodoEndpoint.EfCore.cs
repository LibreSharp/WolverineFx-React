#if (Saas)
using ApiHost.Organisations;
using JasperFx.MultiTenancy;
#endif
#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using ApiHost.Data.EfCore;
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
        Guid id, RemindTodo request, AppDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        // Only the request's organisation's Todos are found.
        var todo = await db.Todos.FindAsync([id], cancellationToken);
        if (todo is null)
#elif (Identity)
    [Authorize]
    [WolverinePost("/api/todos/{id}/remind")]
    public static async Task<(Results<Accepted<TodoReminderScheduled>, NotFound>, OutgoingMessages)> Post(
        Guid id, RemindTodo request, AppDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.FindAsync([id], cancellationToken);
        if (todo is null || todo.OwnerId != user.GetUserId())
#else
    [WolverinePost("/api/todos/{id}/remind")]
    public static async Task<(Results<Accepted<TodoReminderScheduled>, NotFound>, OutgoingMessages)> Post(
        Guid id, RemindTodo request, AppDbContext db, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.FindAsync([id], cancellationToken);
        if (todo is null)
#endif
        {
            return (TypedResults.NotFound(), []);
        }

        var delay = TimeSpan.FromMinutes(request.Minutes);
        var messages = new OutgoingMessages();
#if (Saas)
        messages.Delay(new TodoReminderDue(todo.Id, user.GetUserId()), delay);
#else
        messages.Delay(new TodoReminderDue(todo.Id), delay);
#endif
        return (TypedResults.Accepted((string?)null, new TodoReminderScheduled(DateTimeOffset.UtcNow.Add(delay))), messages);
    }
}

public static class TodoReminderDueHandler
{
#if (Saas)
    // Runs when the reminder is due. Nothing happens if the Todo was completed or deleted in the
    // meantime, or the member who asked for it has left the organisation; otherwise their open
    // tabs show the reminder.
    // tenantId: the organisation the reminder was scheduled in; Wolverine keeps it on the message.
    public static async Task<object?> Handle(
        TodoReminderDue message, AppDbContext db, IOrganisationStore organisations, TenantId tenantId, CancellationToken cancellationToken)
#else
    // Runs when the reminder is due. Nothing happens if the Todo was completed or deleted in
    // the meantime; otherwise the owner's open tabs show the reminder.
    public static async Task<object?> Handle(TodoReminderDue message, AppDbContext db, CancellationToken cancellationToken)
#endif
    {
        var todo = await db.Todos.FindAsync([message.TodoId], cancellationToken);
        if (todo is null || todo.IsComplete)
        {
            return null;
        }

#if (Saas)
        if (await organisations.FindMembershipAsync(Guid.Parse(tenantId.Value), message.UserId, cancellationToken) is null)
        {
            return null;
        }

        return TodoNotifications.Reminder(todo, message.UserId);
#else
        return TodoNotifications.Reminder(todo);
#endif
    }
}
