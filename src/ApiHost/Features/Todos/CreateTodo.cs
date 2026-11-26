#if (IsMarten)
using Wolverine.Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
#endif
#if (Identity)
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
using FluentValidation;
using Wolverine.Http;

namespace ApiHost.Features.Todos;

public record CreateTodo(string Title);

// Discovered by opts.UseFluentValidation() in Program.cs. An invalid request gets a
// 400 ProblemDetails response before the endpoint runs.
public class CreateTodoValidator : AbstractValidator<CreateTodo>
{
    public const int MaxTitleLength = 200;

    public CreateTodoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(MaxTitleLength);
    }
}

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
#if (IsMarten)
    // Event sourcing: a new Todo is a new event stream that starts with TodoCreated.
    // The inline snapshot projection (MartenPersistence) writes the Todo document
    // that GET /api/todos reads, in the same transaction.
#if (Identity)
    public static (TodoCreationResponse, IStartStream) Post(CreateTodo command, ClaimsPrincipal user)
    {
        var id = Guid.CreateVersion7();
        return (new TodoCreationResponse(id), MartenOps.StartStream<Todo>(id, new TodoCreated(command.Title, user.GetUserId())));
    }
#else
    public static (TodoCreationResponse, IStartStream) Post(CreateTodo command)
    {
        var id = Guid.CreateVersion7();
        return (new TodoCreationResponse(id), MartenOps.StartStream<Todo>(id, new TodoCreated(command.Title)));
    }
#endif
#elif (IsEfCore)
#if (Identity)
    public static TodoCreationResponse Post(CreateTodo command, AppDbContext db, ClaimsPrincipal user)
#else
    public static TodoCreationResponse Post(CreateTodo command, AppDbContext db)
#endif
    {
        var todo = new Todo
        {
            Id = Guid.CreateVersion7(),
#if (Identity)
            OwnerId = user.GetUserId(),
#endif
            Title = command.Title,
            IsComplete = false
        };

        db.Todos.Add(todo);

        return new TodoCreationResponse(todo.Id);
    }
#endif
}
