namespace ApiHost.Notifications;

// In Marten or EF Core (NotificationStore).
public interface INotificationStore
{
    // Saves it, and deletes the user's oldest beyond keep.
    Task AddAsync(Notification notification, int keep, CancellationToken cancellationToken);

    // Newest first.
    Task<IReadOnlyList<Notification>> ListAsync(string userId, int count, CancellationToken cancellationToken);

    Task<int> UnreadCountAsync(string userId, CancellationToken cancellationToken);

    // Marks one (or, with id null, all) of the user's notifications read.
    Task MarkReadAsync(string userId, Guid? id, DateTimeOffset at, CancellationToken cancellationToken);

    Task<NotificationPreferences> PreferencesAsync(string userId, CancellationToken cancellationToken);

    Task SavePreferencesAsync(NotificationPreferences preferences, CancellationToken cancellationToken);

    Task DeleteUserAsync(string userId, CancellationToken cancellationToken);
}
