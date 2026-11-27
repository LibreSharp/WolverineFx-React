#if (Saas)
using System.Text.Json.Serialization;
using JasperFx.MultiTenancy;

#endif
namespace ApiHost.Features.Todos;

// Event-sourced: a Todo's state is built from the events in its stream.
// An inline snapshot projection (MartenPersistence) also stores the latest state as a
// Todo document, which is what GET /api/todos queries.
#if (Identity)
public record TodoCreated(string Title, string OwnerId);
#else
public record TodoCreated(string Title);
#endif

#if (Saas)
// ITenanted: each Todo belongs to an organisation (the tenant). Wolverine and
// Marten set and filter TenantId, so the app never does.
public class Todo : ITenanted
#else
public class Todo
#endif
{
    public Guid Id { get; set; }
#if (Saas)

    [JsonIgnore]
    public string? TenantId { get; set; }

#endif
#if (Identity)
    public string OwnerId { get; set; } = string.Empty;
#endif
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }

    // Marten calls Create for the first event in the stream and Apply for each later one.
    public static Todo Create(TodoCreated created) => new()
    {
        Title = created.Title,
#if (Identity)
        OwnerId = created.OwnerId,
#endif
    };

    public void Apply(TodoCompleted completed) => IsComplete = true;
}
