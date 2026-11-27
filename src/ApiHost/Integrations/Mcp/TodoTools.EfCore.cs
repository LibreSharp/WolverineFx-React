using System.ComponentModel;
using ApiHost.Billing;
using ApiHost.Data.EfCore;
using ApiHost.Features.Todos;
using ApiHost.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Wolverine.EntityFrameworkCore;

namespace ApiHost.Integrations.Mcp;

// The organisation's Todos as MCP tools, so an AI assistant given an API key can list, add and
// complete them. Each tool follows the same rules as the HTTP endpoints (validation, plan limits,
// realtime updates) and works in the key's organisation only.
[McpServerToolType]
public class TodoTools(IHttpContextAccessor http, IDbContextOutboxFactory outboxes, Entitlements entitlements)
{
    [McpServerTool(Name = "list_todos", ReadOnly = true), Description("Lists the organisation's Todos, with their ids and whether each is complete.")]
    public async Task<IReadOnlyList<TodoSummary>> ListTodos(CancellationToken cancellationToken)
    {
        var outbox = await OpenAsync(Caller, cancellationToken);
        var todos = await outbox.DbContext.Todos.AsNoTracking().ToListAsync(cancellationToken);
        return [.. todos.Select(TodoSummary.Of)];
    }

    [McpServerTool(Name = "add_todo"), Description("Adds a Todo to the organisation's list, and returns it.")]
    public async Task<TodoSummary> AddTodo([Description("What needs doing (up to 200 characters).")] string title, CancellationToken cancellationToken)
    {
        var caller = Caller.MustWrite();
        TodoToolRules.Validate(title);
        await TodoToolRules.RequireRoomAsync(entitlements, caller.OrganisationId, cancellationToken);

        // The endpoint's own code: it adds the Todo and returns the realtime update for open tabs.
        var outbox = await OpenAsync(caller, cancellationToken);
        var (created, messages) = CreateTodoEndpoint.Post(new CreateTodo(title), outbox.DbContext, caller.User);
        foreach (var message in messages)
        {
            await outbox.PublishAsync(message);
        }

        await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
        return new TodoSummary(created.Id, title, false);
    }

    [McpServerTool(Name = "complete_todo", Idempotent = true), Description("Marks one of the organisation's Todos as complete.")]
    public async Task<TodoSummary> CompleteTodo([Description("The Todo's id, from list_todos.")] Guid id, CancellationToken cancellationToken)
    {
        var caller = Caller.MustWrite();
        var outbox = await OpenAsync(caller, cancellationToken);
        var todo = await outbox.DbContext.Todos.FindAsync([id], cancellationToken)
            ?? throw new McpException($"There is no Todo with id {id}.");

        var (_, messages) = await CompleteTodoEndpoint.Post(id, outbox.DbContext, cancellationToken);
        foreach (var message in messages)
        {
            await outbox.PublishAsync(message);
        }

        await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
        return new TodoSummary(todo.Id, todo.Title, true);
    }

    private TodoToolCaller Caller => TodoToolCaller.Of(http);

    // A DbContext for the key's organisation (it only sees that organisation's Todos), whose
    // messages go out through the outbox when it saves.
    private ValueTask<IDbContextOutbox<AppDbContext>> OpenAsync(TodoToolCaller caller, CancellationToken cancellationToken)
        => outboxes.CreateForTenantAsync<AppDbContext>(caller.OrganisationId.ToString(), cancellationToken);
}
