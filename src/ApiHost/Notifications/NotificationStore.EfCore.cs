using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Notifications;

// Notifications and preferences are ordinary (not tenanted) tables (Data/EfCore/AppDbContext.cs).
public class NotificationStore(AppDbContext db) : INotificationStore
{
    public async Task AddAsync(Notification notification, int keep, CancellationToken cancellationToken)
    {
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);
        var oldest = await db.Notifications
            .Where(n => n.UserId == notification.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(keep)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);
        if (oldest.Count > 0)
        {
            await db.Notifications.Where(n => oldest.Contains(n.Id)).ExecuteDeleteAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Notification>> ListAsync(string userId, int count, CancellationToken cancellationToken)
        => await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public Task<int> UnreadCountAsync(string userId, CancellationToken cancellationToken)
        => db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);

    public Task MarkReadAsync(string userId, Guid? id, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var unread = db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null);
        if (id is { } one)
        {
            unread = unread.Where(n => n.Id == one);
        }

        return unread.ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, at), cancellationToken);
    }

    public async Task<NotificationPreferences> PreferencesAsync(string userId, CancellationToken cancellationToken)
        => await db.NotificationPreferences.FirstOrDefaultAsync(p => p.Id == userId, cancellationToken) ?? new NotificationPreferences { Id = userId };

    public Task SavePreferencesAsync(NotificationPreferences preferences, CancellationToken cancellationToken)
    {
        if (db.Entry(preferences).State == EntityState.Detached)
        {
            db.NotificationPreferences.Add(preferences);
        }

        return db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        await db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.NotificationPreferences.Where(p => p.Id == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
