using ApiHost.DataExport;
using Marten;

namespace ApiHost.Features.Todos;

// The user's Todos, in "Download your data".
public class TodosPersonalData(IQuerySession session) : IPersonalDataSource
{
    public string Section => "todos";

    public async Task<object?> ExportAsync(string userId, CancellationToken cancellationToken)
        => await session.Query<Todo>().Where(t => t.OwnerId == userId).ToListAsync(cancellationToken);
}
