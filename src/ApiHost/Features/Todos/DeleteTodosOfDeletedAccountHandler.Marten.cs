using ApiHost.Identity;
using Marten;

namespace ApiHost.Features.Todos;

// Deletes a user's Todos when they delete their account (AccountDeleted is published by
// Features/Account/DeleteAccountEndpoint.cs). Runs in the background through Wolverine's
// durable outbox; the changes are saved automatically (AutoApplyTransactions).
// Wolverine finds handlers by name: the class name must end in "Handler" (or "Consumer").
public static class DeleteTodosOfDeletedAccountHandler
{
    public static async Task Handle(AccountDeleted message, IDocumentSession session, CancellationToken cancellationToken)
    {
        var todoIds = await session.Query<Todo>()
            .Where(t => t.OwnerId == message.UserId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in todoIds)
        {
            // Events hold the user's data too (titles, owner id), so permanently delete each
            // Todo's event stream, not just the snapshot document.
            session.Events.TombstoneStream(id);
            session.HardDelete<Todo>(id);
        }
    }
}
