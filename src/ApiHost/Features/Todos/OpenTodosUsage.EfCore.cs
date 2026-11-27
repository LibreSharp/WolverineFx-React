using ApiHost.Billing;
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Features.Todos;

// The OpenTodos limit: the organisation's Todos that aren't complete. Completing one makes room.
// The DbContext here isn't pinned to an organisation, so the query names it.
public class OpenTodosUsage(AppDbContext db) : ILimitUsage
{
    public string Limit => PlanLimits.OpenTodos;

    public Task<int> CountAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        var tenant = organisationId.ToString();
        return db.Todos.IgnoreQueryFilters().CountAsync(t => t.TenantId == tenant && !t.IsComplete, cancellationToken);
    }
}
