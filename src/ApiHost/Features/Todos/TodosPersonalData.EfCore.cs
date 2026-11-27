using ApiHost.Data.EfCore;
using ApiHost.DataExport;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Features.Todos;

// The user's Todos, in "Download your data".
public class TodosPersonalData(AppDbContext db) : IPersonalDataSource
{
    public string Section => "todos";

    public async Task<object?> ExportAsync(string userId, CancellationToken cancellationToken)
        => await db.Todos.AsNoTracking().Where(t => t.OwnerId == userId).ToListAsync(cancellationToken);
}
