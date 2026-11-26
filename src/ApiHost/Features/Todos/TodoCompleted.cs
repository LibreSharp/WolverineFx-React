namespace ApiHost.Features.Todos;

#if (IsMarten)
// Appended to the Todo's event stream, and also published as a message so
// TodoCompletedHandler can react to it in the background.
#else
// Published as a message when a Todo is completed, so TodoCompletedHandler can
// react to it in the background.
#endif
public record TodoCompleted(Guid TodoId, string Title);

// Runs asynchronously, after the endpoint that completed the Todo has returned.
// Local queues are durable (see Program.cs), so the message is stored by the
// transactional outbox in the same database transaction as the Todo change: it is
// never lost if the app stops, and never sent if that transaction rolls back.
// Replace the log line with real follow-up work, such as an email, a notification,
// or an integration call.
public class TodoCompletedHandler
{
    public static void Handle(TodoCompleted message, ILogger<TodoCompletedHandler> logger)
    {
        logger.LogInformation("Todo {TodoId} ('{Title}') was completed", message.TodoId, message.Title);
    }
}
