using ApiHost.Data.EfCore;
using ApiHost.Organisations;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Features.Todos;

// Deletes a deleted organisation's Todos. The message is sent for that organisation's tenant, so
// Wolverine pins the DbContext to it; the explicit filter on TenantId is a second guard.
public static class DeleteTodosOfDeletedOrganisationHandler
{
    public static async Task Handle(OrganisationDeleted message, AppDbContext db, CancellationToken cancellationToken)
    {
        var organisationId = message.OrganisationId.ToString();
        await db.Todos.Where(t => t.TenantId == organisationId).ExecuteDeleteAsync(cancellationToken);
    }
}
