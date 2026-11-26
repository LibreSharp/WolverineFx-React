namespace ApiHost.Features.Todos;

#if (IsMarten)
// Appended to the Todo's event stream, and also published as a message so
// TodoCompletedHandler can react to it in the background.
#else
// Published as a message when a Todo is completed, so TodoCompletedHandler can
// react to it in the background.
#endif
#if (Identity)
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
    public static object Handle(TodoCompleted message, ILogger<TodoCompletedHandler> logger)
    {
        logger.LogInformation("Todo {TodoId} ('{Title}') was completed", message.TodoId, message.Title);
#if (Identity)
        return TodoNotifications.Changed(message.TodoId, message.OwnerId);
#else
        return TodoNotifications.Changed(message.TodoId);
#endif
    }
}
