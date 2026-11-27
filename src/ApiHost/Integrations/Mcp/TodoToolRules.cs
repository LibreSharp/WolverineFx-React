using System.Security.Claims;
using ApiHost.Billing;
using ApiHost.Features.Todos;
using ApiHost.Integrations.ApiKeys;
using ApiHost.Organisations;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol;
using Wolverine.Http;

namespace ApiHost.Integrations.Mcp;

// What an MCP tool returns about a Todo.
public record TodoSummary(Guid Id, string Title, bool IsComplete)
{
    public static TodoSummary Of(Todo todo) => new(todo.Id, todo.Title, todo.IsComplete);
}

// The API key a tool call was made with (the MCP endpoint only accepts API keys).
public record TodoToolCaller(ClaimsPrincipal User, Guid OrganisationId)
{
    public static TodoToolCaller Of(IHttpContextAccessor http)
    {
        var user = http.HttpContext?.User ?? throw new McpException("No request.");
        return new TodoToolCaller(user, user.GetOrganisationId());
    }

    public TodoToolCaller MustWrite()
        => User.CanWrite() ? this : throw new McpException("This API key can only read: create a key with the write scope.");
}

// The HTTP endpoints' rules, for the tools. A tool reports a broken rule as an error the assistant
// can read and act on.
public static class TodoToolRules
{
    public static void Validate(string title)
    {
        var result = new CreateTodoValidator().Validate(new CreateTodo(title));
        if (!result.IsValid)
        {
            throw new McpException(string.Join(" ", result.Errors.Select(e => e.ErrorMessage)));
        }
    }

    public static async Task RequireRoomAsync(Entitlements entitlements, Guid organisationId, CancellationToken cancellationToken)
    {
        var problem = await entitlements.RequireRoomAsync(organisationId, PlanLimits.OpenTodos, 1, cancellationToken);
        if (!ReferenceEquals(problem, WolverineContinue.NoProblems))
        {
            throw new McpException(problem.Detail ?? "The organisation's plan has no room for another Todo.");
        }
    }
}
