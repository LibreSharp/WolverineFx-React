using ApiHost.Billing;
using Marten;

namespace ApiHost.Features.Todos;

// The OpenTodos limit: the organisation's Todos that aren't complete. Completing one makes room.
public class OpenTodosUsage(IDocumentStore store) : ILimitUsage
{
    public string Limit => PlanLimits.OpenTodos;

    public async Task<int> CountAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession(organisationId.ToString());
        return await session.Query<Todo>().CountAsync(t => !t.IsComplete, cancellationToken);
    }
}
