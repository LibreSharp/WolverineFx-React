using Marten;
using Marten.Patching;

namespace ApiHost.Notifications;

// Notifications and preferences are ordinary (not tenanted) Marten documents. Writes from message
// handlers use sessions of their own, as a handler's session can only be saved once.
public class NotificationStore(IDocumentSession session, IDocumentStore store) : INotificationStore
{
    public async Task AddAsync(Notification notification, int keep, CancellationToken cancellationToken)
    {
        await using var write = store.LightweightSession();
        write.Store(notification);
        var older = await write.Query<Notification>()
            .Where(n => n.UserId == notification.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(keep - 1)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in older)
        {
            write.Delete<Notification>(id);
        }

        await write.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> ListAsync(string userId, int count, CancellationToken cancellationToken)
        => await session.Query<Notification>()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public Task<int> UnreadCountAsync(string userId, CancellationToken cancellationToken)
        => session.Query<Notification>().CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);

    public Task MarkReadAsync(string userId, Guid? id, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var patch = id is { } one
            ? session.Patch<Notification>(n => n.Id == one && n.UserId == userId && n.ReadAt == null)
            : session.Patch<Notification>(n => n.UserId == userId && n.ReadAt == null);
        patch.Set(n => n.ReadAt, at);
        return session.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationPreferences> PreferencesAsync(string userId, CancellationToken cancellationToken)
        => await session.LoadAsync<NotificationPreferences>(userId, cancellationToken) ?? new NotificationPreferences { Id = userId };

    public Task SavePreferencesAsync(NotificationPreferences preferences, CancellationToken cancellationToken)
    {
        session.Store(preferences);
        return session.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        await using var delete = store.LightweightSession();
        delete.DeleteWhere<Notification>(n => n.UserId == userId);
        delete.Delete<NotificationPreferences>(userId);
        await delete.SaveChangesAsync(cancellationToken);
    }
}
