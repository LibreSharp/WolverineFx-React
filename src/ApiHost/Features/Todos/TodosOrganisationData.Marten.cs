using ApiHost.DataExport;
using Marten;

namespace ApiHost.Features.Todos;

// The organisation's Todos, in its data download.
public class TodosOrganisationData(IDocumentStore store) : IOrganisationDataSource
{
    public string Section => "todos";

    public async Task<object?> ExportAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession(organisationId.ToString());
        return await session.Query<Todo>().ToListAsync(cancellationToken);
    }
}
