using ApiHost.Data.EfCore;
using ApiHost.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Features.Todos;

// Deletes a user's Todos when they delete their account (AccountDeleted is published by
// Features/Account/DeleteAccountEndpoint.cs). Runs in the background through Wolverine's
// durable outbox; the changes are saved automatically (AutoApplyTransactions).
// Wolverine finds handlers by name: the class name must end in "Handler" (or "Consumer").
public static class DeleteTodosOfDeletedAccountHandler
{
    public static async Task Handle(AccountDeleted message, AppDbContext db, CancellationToken cancellationToken)
    {
        var todos = await db.Todos.Where(t => t.OwnerId == message.UserId).ToListAsync(cancellationToken);
        db.Todos.RemoveRange(todos);
    }
}
