using FluentValidation;
using Wolverine.Attributes;

namespace ApiHost.Features.Todos;

// "Remind me later": an example of scheduled background work with Wolverine. The endpoint and
// the handler that runs when the reminder is due are in RemindTodoEndpoint.

public record RemindTodo(int Minutes);

public class RemindTodoValidator : AbstractValidator<RemindTodo>
{
    public const int MaxMinutes = 7 * 24 * 60;

    public RemindTodoValidator()
    {
        RuleFor(x => x.Minutes).InclusiveBetween(1, MaxMinutes);
    }
}

public record TodoReminderScheduled(DateTimeOffset RemindAt);

// Scheduled for later; handled by TodoReminderDueHandler when it's due.
public record TodoReminderDue(Guid TodoId);

// Pushed to the browser over SignalR when a reminder is due; the client matches on the
// stable "todo-reminder" name.
[MessageIdentity("todo-reminder")]
public record TodoReminder(Guid TodoId, string Title);
