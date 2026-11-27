using System.ComponentModel;
using ApiHost.Billing;
using ApiHost.Features.Todos;
using ApiHost.Identity;
using ApiHost.Integrations.Webhooks;
using Marten;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Wolverine;
using Wolverine.Marten.Publishing;

namespace ApiHost.Integrations.Mcp;

// The organisation's Todos as MCP tools, so an AI assistant given an API key can list, add and
// complete them. Each tool follows the same rules as the HTTP endpoints (validation, plan limits,
// realtime updates) and works in the key's organisation only.
[McpServerToolType]
public class TodoTools(IHttpContextAccessor http, IDocumentStore store, OutboxedSessionFactory sessions, IMessageBus bus, Entitlements entitlements)
{
    [McpServerTool(Name = "list_todos", ReadOnly = true), Description("Lists the organisation's Todos, with their ids and whether each is complete.")]
    public async Task<IReadOnlyList<TodoSummary>> ListTodos(CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession(Caller.OrganisationId.ToString());
        var todos = await session.Query<Todo>().ToListAsync(cancellationToken);
        return [.. todos.Select(TodoSummary.Of)];
    }

    [McpServerTool(Name = "add_todo"), Description("Adds a Todo to the organisation's list, and returns it.")]
    public async Task<TodoSummary> AddTodo([Description("What needs doing (up to 200 characters).")] string title, CancellationToken cancellationToken)
    {
        var caller = Caller.MustWrite();
        TodoToolRules.Validate(title);
        await TodoToolRules.RequireRoomAsync(entitlements, caller.OrganisationId, cancellationToken);

        // As CreateTodoEndpoint does: a new event stream, a realtime update for open tabs, and an
        // event for the organisation's webhooks.
        var id = Guid.CreateVersion7();
        await using var session = OpenSession(caller);
        session.Events.StartStream<Todo>(id, new TodoCreated(title, caller.User.GetUserId()));
        await PublishAsync([
            TodoNotifications.Changed(id, caller.OrganisationId.ToString()),
            WebhookEvents.Raise(caller.OrganisationId, WebhookEvents.TodoCreated, new TodoWebhookData(id, title, false)),
        ]);
        await session.SaveChangesAsync(cancellationToken);
        return new TodoSummary(id, title, false);
    }

    [McpServerTool(Name = "complete_todo", Idempotent = true), Description("Marks one of the organisation's Todos as complete.")]
    public async Task<TodoSummary> CompleteTodo([Description("The Todo's id, from list_todos.")] Guid id, CancellationToken cancellationToken)
    {
        var caller = Caller.MustWrite();
        await using var session = OpenSession(caller);
        var stream = await session.Events.FetchForWriting<Todo>(id, cancellationToken);
        var todo = stream.Aggregate ?? throw new McpException($"There is no Todo with id {id}.");

        // The endpoint's own decision: no event if it's already complete.
        var (_, events, messages) = CompleteTodoEndpoint.Post(todo);
        stream.AppendMany(events.ToArray());
        await PublishAsync(messages);
        await session.SaveChangesAsync(cancellationToken);
        return new TodoSummary(todo.Id, todo.Title, true);
    }

    private TodoToolCaller Caller => TodoToolCaller.Of(http);

    // A session for the key's organisation whose messages go out through the outbox when it saves.
    private IDocumentSession OpenSession(TodoToolCaller caller)
    {
        bus.TenantId = caller.OrganisationId.ToString();
        return sessions.OpenSession(bus);
    }

    private async Task PublishAsync(IEnumerable<object> messages)
    {
        foreach (var message in messages)
        {
            await bus.PublishAsync(message);
        }
    }
}
