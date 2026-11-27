#if (Saas)
using ApiHost.Billing;
using ApiHost.Organisations;
using JasperFx.MultiTenancy;
using Microsoft.AspNetCore.Mvc;
#endif
#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using ApiHost.Notifications;
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
    // Reminders are a plan feature (PlanFeatures.TodoReminders): without it, 402 Payment Required.
    public static Task<ProblemDetails> BeforeAsync(ClaimsPrincipal user, Entitlements entitlements, CancellationToken cancellationToken)
        => entitlements.RequireFeatureAsync(user.GetOrganisationId(), PlanFeatures.TodoReminders, cancellationToken);

    // Each reminder uses one of the plan's reminders for the month (PlanLimits.Reminders).
    [Authorize]
    [WolverinePost("/api/todos/{id}/remind")]
    public static async Task<(Results<Accepted<TodoReminderScheduled>, NotFound, ProblemHttpResult>, OutgoingMessages)> Post(
        Guid id, RemindTodo request, AppDbContext db, ClaimsPrincipal user, Entitlements entitlements, CancellationToken cancellationToken)
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

#if (Saas)
        var organisationId = user.GetOrganisationId();
        if (!await entitlements.TryUseAsync(organisationId, PlanLimits.Reminders, 1, cancellationToken))
        {
            return (TypedResults.Problem(await entitlements.LimitReachedAsync(organisationId, PlanLimits.Reminders, cancellationToken)), []);
        }

#endif
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
    // meantime, the member who asked for it has left the organisation, or the organisation's plan
    // no longer includes reminders (it was downgraded); otherwise their open tabs show the reminder.
    // tenantId: the organisation the reminder was scheduled in; Wolverine keeps it on the message.
    public static async Task<OutgoingMessages> Handle(
        TodoReminderDue message, AppDbContext db, IOrganisationStore organisations, Entitlements entitlements, TenantId tenantId, CancellationToken cancellationToken)
#else
    // Runs when the reminder is due. Nothing happens if the Todo was completed or deleted in
    // the meantime; otherwise the owner's open tabs show the reminder.
    public static async Task<OutgoingMessages> Handle(TodoReminderDue message, AppDbContext db, CancellationToken cancellationToken)
#endif
    {
        var todo = await db.Todos.FindAsync([message.TodoId], cancellationToken);
        if (todo is null || todo.IsComplete)
        {
            return [];
        }

#if (Saas)
        var organisationId = Guid.Parse(tenantId.Value);
        if (await organisations.FindMembershipAsync(organisationId, message.UserId, cancellationToken) is null
            || !await entitlements.IsEnabledAsync(organisationId, PlanFeatures.TodoReminders, cancellationToken))
        {
            return [];
        }

        // The reminder in their open tabs, and a notification (listed in the header, and emailed).
        return [TodoNotifications.Reminder(todo, message.UserId), Notify(todo, message.UserId)];
#elif (Identity)
        return [TodoNotifications.Reminder(todo), Notify(todo, todo.OwnerId)];
#else
        return [TodoNotifications.Reminder(todo)];
#endif
    }
#if (Identity)

    private static SendNotification Notify(Todo todo, string userId)
        => new(userId, NotificationCategories.Reminders, $"Reminder: {todo.Title}", "You asked to be reminded about this Todo.", "/todos");
#endif
}
