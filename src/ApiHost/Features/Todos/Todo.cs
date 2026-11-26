namespace ApiHost.Features.Todos;

#if (IsMarten)
// Event-sourced: a Todo's state is built from the events in its stream.
// An inline snapshot projection (MartenPersistence) also stores the latest state as a
// Todo document, which is what GET /api/todos queries.
#if (Identity)
public record TodoCreated(string Title, string OwnerId);
#else
public record TodoCreated(string Title);
#endif

#endif
public class Todo
{
    public Guid Id { get; set; }
#if (Identity)
    public string OwnerId { get; set; } = string.Empty;
#endif
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
#if (IsMarten)

    // Marten calls Create for the first event in the stream and Apply for each later one.
    public static Todo Create(TodoCreated created) => new()
    {
        Title = created.Title,
#if (Identity)
        OwnerId = created.OwnerId,
#endif
    };

    public void Apply(TodoCompleted completed) => IsComplete = true;
#endif
}
