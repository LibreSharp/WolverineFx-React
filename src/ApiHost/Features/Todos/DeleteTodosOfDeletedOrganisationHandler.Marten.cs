using ApiHost.Organisations;
using Marten;

namespace ApiHost.Features.Todos;

// Deletes a deleted organisation's Todos. The message is sent for that organisation's tenant, so
// Wolverine opens the session for it and the query only finds its Todos.
public static class DeleteTodosOfDeletedOrganisationHandler
{
    public static async Task Handle(OrganisationDeleted message, IDocumentSession session, CancellationToken cancellationToken)
    {
        var todoIds = await session.Query<Todo>().Select(t => t.Id).ToListAsync(cancellationToken);
        foreach (var id in todoIds)
        {
            // Events hold the organisation's data too, so permanently delete each event stream,
            // not just the snapshot document.
            session.Events.TombstoneStream(id);
            session.HardDelete<Todo>(id);
        }
    }
}
