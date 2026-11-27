using ApiHost.Data.EfCore;
using ApiHost.DataExport;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace ApiHost.Features.Todos;

// The organisation's Todos, in its data download.
public class TodosOrganisationData(IDbContextOutboxFactory contexts) : IOrganisationDataSource
{
    public string Section => "todos";

    public async Task<object?> ExportAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        // A DbContext for that organisation: it only sees its Todos.
        var outbox = await contexts.CreateForTenantAsync<AppDbContext>(organisationId.ToString(), cancellationToken);
        return await outbox.DbContext.Todos.AsNoTracking().ToListAsync(cancellationToken);
    }
}
