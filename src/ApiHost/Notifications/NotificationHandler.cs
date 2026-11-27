using ApiHost.Features.Todos;
using ApiHost.Identity;
using ApiHost.Identity.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.SignalR;

namespace ApiHost.Notifications;

// The link in a notification email that turns that kind of email off with one click: an
// encrypted "user and category", so it needs no login and can't be made up for someone else.
public class UnsubscribeTokens(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Notifications.Unsubscribe");

    public string Create(string userId, string category) => _protector.Protect($"{userId}\n{category}");

    public (string UserId, string Category)? Read(string token)
    {
        try
        {
            var parts = _protector.Unprotect(token).Split('\n');
            return parts.Length == 2 ? (parts[0], parts[1]) : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}

// Sent by NotificationHandler on its own, so a mail server that's down retries only the email.
public record EmailNotification(string UserId, string Category, string Title, string? Body, string? Link);

public static class NotificationHandler
{
    // Stores the notification and refreshes the user's open tabs; then emails it, unless they
    // turned that category's emails off.
    public static async Task<OutgoingMessages> Handle(
        SendNotification message, INotificationStore store, IOptions<NotificationOptions> options, TimeProvider clock, CancellationToken cancellationToken)
    {
        var notification = new Notification
        {
            Id = Guid.CreateVersion7(),
            UserId = message.UserId,
            Category = message.Category,
            Title = message.Title,
            Body = message.Body,
            Link = message.Link,
            CreatedAt = clock.GetUtcNow(),
        };
        await store.AddAsync(notification, options.Value.KeepPerUser, cancellationToken);

        OutgoingMessages messages = [new NotificationAdded(notification.Id).ToWebSocketGroup(TodoNotifications.UserGroup(message.UserId))];
        var preferences = await store.PreferencesAsync(message.UserId, cancellationToken);
        if (!preferences.EmailOff.Contains(message.Category, StringComparer.Ordinal))
        {
            messages.Add(new EmailNotification(message.UserId, message.Category, message.Title, message.Body, message.Link));
        }

        return messages;
    }

    // With a one-click unsubscribe link (the List-Unsubscribe headers mail apps show as a button)
    // and one in the text. Only to a confirmed address.
    public static async Task Handle(
        EmailNotification message, UserManager<ApplicationUser> users, IEmailSender email, UnsubscribeTokens unsubscribe, IConfiguration configuration, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(message.UserId) is not { EmailConfirmed: true, Email: { } address })
        {
            return;
        }

        var clientUrl = configuration[ClientUrl.Setting]?.TrimEnd('/') ?? "http://localhost:5173";
        var token = Uri.EscapeDataString(unsubscribe.Create(message.UserId, message.Category));
        var body = string.Join("\n\n", new[]
        {
            message.Body,
            message.Link is null ? null : $"Open it: {clientUrl}{message.Link}",
            $"Don't want these emails? Unsubscribe: {clientUrl}/unsubscribe?token={token}",
        }.Where(part => part is not null));
        await email.SendAsync(new EmailMessage(address, message.Title, body, $"{clientUrl}/api/notifications/unsubscribe?token={token}"), cancellationToken);
    }

    // A deleted account's notifications go with it.
    public static Task Handle(AccountDeleted message, INotificationStore store, CancellationToken cancellationToken)
        => store.DeleteUserAsync(message.UserId, cancellationToken);
}
