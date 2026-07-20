#if (IsMarten)
using Marten;
#elif (IsEfCore)
using Api.Data.EfCore;
#endif
using Wolverine.Http;

namespace Api.Features.Todos;

public record CreateTodo(string Title);

public record TodoCreationResponse : CreationResponse
{
    public TodoCreationResponse(Guid id) : base($"/api/todos/{id}")
    {
        Id = id;
    }

    public Guid Id { get; }
}

public static class CreateTodoEndpoint
{
    [WolverinePost("/api/todos")]
#if (IsMarten)
    public static TodoCreationResponse Post(CreateTodo command, IDocumentSession session)
#elif (IsEfCore)
    public static TodoCreationResponse Post(CreateTodo command, AppDbContext db)
#endif
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            Title = command.Title,
            IsComplete = false
        };

#if (IsMarten)
        session.Store(todo);
#elif (IsEfCore)
        db.Todos.Add(todo);
#endif

        return new TodoCreationResponse(todo.Id);
    }
}
