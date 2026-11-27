using Wolverine.Attributes;

namespace ApiHost.Notifications;

// Something the app tells a user about, such as a Todo reminder: shown in the header's list, and
// also emailed unless the user turned that kind of email off. Not tenanted: it's the user's.
public class Notification
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    // One of NotificationCategories.
    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    // A page of the client to open, such as "/todos".
    public string? Link { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}

// A user's choices of which kinds of notification are also emailed. Id: the user's id.
public class NotificationPreferences
{
    public string Id { get; set; } = string.Empty;

    // Categories the user doesn't want emails for (everything else is emailed).
    public IList<string> EmailOff { get; set; } = [];
}

// The kinds of notification. Add one (with its description) for each new kind a feature sends.
public static class NotificationCategories
{
    public const string Reminders = "reminders";
#if (Saas)
    public const string Members = "members";
#endif

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Reminders] = "Todo reminders you set",
#if (Saas)
        [Members] = "Someone joins an organisation you manage",
#endif
    };
}

// Publish this (from an endpoint or handler, through the outbox) to notify a user;
// NotificationHandler stores it, updates their open tabs and emails them.
public record SendNotification(string UserId, string Category, string Title, string? Body = null, string? Link = null);

// Pushed to the user's open tabs over SignalR, so the header's list refreshes; the client matches
// on the stable "notification-added" name.
[MessageIdentity("notification-added")]
public record NotificationAdded(Guid Id);

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    // How many a user keeps: older ones are deleted as new ones arrive.
    public int KeepPerUser { get; set; } = 100;
}
