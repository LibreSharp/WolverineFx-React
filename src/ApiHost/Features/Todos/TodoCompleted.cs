#if (Saas)
using ApiHost.Integrations.Webhooks;
using JasperFx.MultiTenancy;
using Wolverine;

#endif
namespace ApiHost.Features.Todos;

#if (IsMarten)
// Appended to the Todo's event stream, and also published as a message so
// TodoCompletedHandler can react to it in the background.
#else
// Published as a message when a Todo is completed, so TodoCompletedHandler can
// react to it in the background.
#endif
#if (Identity && !Saas)
public record TodoCompleted(Guid TodoId, string Title, string OwnerId);
#else
public record TodoCompleted(Guid TodoId, string Title);
#endif

// Runs asynchronously, after the endpoint that completed the Todo has returned.
// Local queues are durable (see Program.cs), so the message is stored by the
// transactional outbox in the same database transaction as the Todo change: it is
// never lost if the app stops, and never sent if that transaction rolls back.
// The returned TodoChanged is cascaded on to the browser over SignalR. Add real
// follow-up work here too, such as an email, a notification, or an integration call.
public class TodoCompletedHandler
{
#if (Saas)
    // tenantId: the organisation the Todo belongs to; Wolverine carries it on every message.
    public static OutgoingMessages Handle(TodoCompleted message, TenantId tenantId, ILogger<TodoCompletedHandler> logger)
#else
    public static object Handle(TodoCompleted message, ILogger<TodoCompletedHandler> logger)
#endif
    {
        logger.LogInformation("Todo {TodoId} ('{Title}') was completed", message.TodoId, message.Title);
#if (Saas)
        // A realtime update for open tabs, and an event for the organisation's webhooks.
        return [
            TodoNotifications.Changed(message.TodoId, tenantId.Value),
            WebhookEvents.Raise(Guid.Parse(tenantId.Value), WebhookEvents.TodoCompleted, new TodoWebhookData(message.TodoId, message.Title, true)),
        ];
#elif (Identity)
        return TodoNotifications.Changed(message.TodoId, message.OwnerId);
#else
        return TodoNotifications.Changed(message.TodoId);
#endif
    }
}
