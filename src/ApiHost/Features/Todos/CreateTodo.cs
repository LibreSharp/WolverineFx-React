using FluentValidation;
using Wolverine.Http;

namespace ApiHost.Features.Todos;

// The request and response of POST /api/todos (CreateTodoEndpoint).
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
