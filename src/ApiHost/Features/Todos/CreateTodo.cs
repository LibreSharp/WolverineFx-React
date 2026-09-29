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
using Wolverine.Http;

namespace ApiHost.Features.Todos;

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
#if (Identity)
    [Authorize]
#endif
    [WolverinePost("/api/todos")]
#if (IsMarten && Identity)
    public static TodoCreationResponse Post(CreateTodo command, IDocumentSession session, ClaimsPrincipal user)
#elif (IsMarten)
    public static TodoCreationResponse Post(CreateTodo command, IDocumentSession session)
#elif (IsEfCore && Identity)
    public static TodoCreationResponse Post(CreateTodo command, AppDbContext db, ClaimsPrincipal user)
#elif (IsEfCore)
    public static TodoCreationResponse Post(CreateTodo command, AppDbContext db)
#endif
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
#if (Identity)
            OwnerId = user.GetUserId(),
#endif
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
