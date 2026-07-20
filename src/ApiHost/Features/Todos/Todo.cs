namespace ApiHost.Features.Todos;

public class Todo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}
