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
using Wolverine;
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
    // that GET /api/todos reads, in the same transaction. The TodoChanged notification
    // goes through the outbox, so open tabs only hear about the Todo once it is saved.
#if (Identity)
    public static (TodoCreationResponse, IStartStream, OutgoingMessages) Post(CreateTodo command, ClaimsPrincipal user)
    {
        var id = Guid.CreateVersion7();
        var ownerId = user.GetUserId();
        return (
            new TodoCreationResponse(id),
            MartenOps.StartStream<Todo>(id, new TodoCreated(command.Title, ownerId)),
            [TodoNotifications.Changed(id, ownerId)]);
    }
#else
    public static (TodoCreationResponse, IStartStream, OutgoingMessages) Post(CreateTodo command)
    {
        var id = Guid.CreateVersion7();
        return (
            new TodoCreationResponse(id),
            MartenOps.StartStream<Todo>(id, new TodoCreated(command.Title)),
            [TodoNotifications.Changed(id)]);
    }
#endif
#elif (IsEfCore)
#if (Identity)
    public static (TodoCreationResponse, OutgoingMessages) Post(CreateTodo command, AppDbContext db, ClaimsPrincipal user)
#else
    public static (TodoCreationResponse, OutgoingMessages) Post(CreateTodo command, AppDbContext db)
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

        // Saved to the outbox with the new row, so open tabs only hear about it once it exists.
#if (Identity)
        return (new TodoCreationResponse(todo.Id), [TodoNotifications.Changed(todo.Id, todo.OwnerId)]);
#else
        return (new TodoCreationResponse(todo.Id), [TodoNotifications.Changed(todo.Id)]);
#endif
    }
#endif
}
