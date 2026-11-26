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
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;
using Wolverine.Attributes;
using Wolverine.Http;

namespace ApiHost.Features.Todos;

// "Remind me later": an example of scheduled background work with Wolverine.

public record RemindTodo(int Minutes);

public class RemindTodoValidator : AbstractValidator<RemindTodo>
{
    public const int MaxMinutes = 7 * 24 * 60;

    public RemindTodoValidator()
    {
        RuleFor(x => x.Minutes).InclusiveBetween(1, MaxMinutes);
    }
}

public record TodoReminderScheduled(DateTimeOffset RemindAt);

// Scheduled for later; handled by TodoReminderDueHandler when it's due.
public record TodoReminderDue(Guid TodoId);

// Pushed to the browser over SignalR when a reminder is due; the client matches on the
// stable "todo-reminder" name.
[MessageIdentity("todo-reminder")]
public record TodoReminder(Guid TodoId, string Title);

public static class RemindTodoEndpoint
{
    // Schedules a TodoReminderDue message. Local queues are durable (Program.cs), so scheduled
    // messages are stored in PostgreSQL and still fire after a restart.
#if (Identity)
    [Authorize]
#endif
    [WolverinePost("/api/todos/{id}/remind")]
#if (IsMarten && Identity)
    public static async Task<(Results<Accepted<TodoReminderScheduled>, NotFound>, OutgoingMessages)> Post(
        Guid id, RemindTodo request, IQuerySession session, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var todo = await session.LoadAsync<Todo>(id, cancellationToken);
        if (todo is null || todo.OwnerId != user.GetUserId())
#elif (IsMarten)
    public static async Task<(Results<Accepted<TodoReminderScheduled>, NotFound>, OutgoingMessages)> Post(
        Guid id, RemindTodo request, IQuerySession session, CancellationToken cancellationToken)
    {
        var todo = await session.LoadAsync<Todo>(id, cancellationToken);
        if (todo is null)
#elif (IsEfCore && Identity)
    public static async Task<(Results<Accepted<TodoReminderScheduled>, NotFound>, OutgoingMessages)> Post(
        Guid id, RemindTodo request, AppDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.FindAsync([id], cancellationToken);
        if (todo is null || todo.OwnerId != user.GetUserId())
#elif (IsEfCore)
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
        messages.Delay(new TodoReminderDue(todo.Id), delay);
        return (TypedResults.Accepted((string?)null, new TodoReminderScheduled(DateTimeOffset.UtcNow.Add(delay))), messages);
    }
}

public static class TodoReminderDueHandler
{
    // Runs when the reminder is due. Nothing happens if the Todo was completed or deleted in
    // the meantime; otherwise the owner's open tabs show the reminder.
#if (IsMarten)
    public static async Task<object?> Handle(TodoReminderDue message, IQuerySession session, CancellationToken cancellationToken)
    {
        var todo = await session.LoadAsync<Todo>(message.TodoId, cancellationToken);
#elif (IsEfCore)
    public static async Task<object?> Handle(TodoReminderDue message, AppDbContext db, CancellationToken cancellationToken)
    {
        var todo = await db.Todos.FindAsync([message.TodoId], cancellationToken);
#endif
        if (todo is null || todo.IsComplete)
        {
            return null;
        }

        return TodoNotifications.Reminder(todo);
    }
}
