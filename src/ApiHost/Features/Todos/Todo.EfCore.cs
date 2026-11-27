namespace ApiHost.Features.Todos;

// A row in the Todos table (Data/EfCore/AppDbContext.cs).
public class Todo
{
    public Guid Id { get; set; }
#if (Identity)
    public string OwnerId { get; set; } = string.Empty;
#endif
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}
