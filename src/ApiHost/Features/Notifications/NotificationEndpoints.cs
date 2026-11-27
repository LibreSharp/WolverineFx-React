using System.Security.Claims;
using ApiHost.Identity;
using ApiHost.Notifications;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Features.Notifications;

public record NotificationSummary(Guid Id, string Category, string Title, string? Body, string? Link, DateTimeOffset CreatedAt, bool Read)
{
    public static NotificationSummary Of(Notification n) => new(n.Id, n.Category, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt is not null);
}

public record NotificationList(IReadOnlyList<NotificationSummary> Notifications, int Unread);

public record EmailPreference(string Category, string Description, bool Email);

public record EmailPreferences(IReadOnlyList<EmailPreference> Categories);

public record UpdateEmailPreferencesRequest(IReadOnlyList<EmailPreference> Categories);

public class UpdateEmailPreferencesRequestValidator : AbstractValidator<UpdateEmailPreferencesRequest>
{
    public UpdateEmailPreferencesRequestValidator()
    {
        RuleForEach(x => x.Categories).Must(c => NotificationCategories.Descriptions.ContainsKey(c.Category))
            .WithMessage("Unknown notification category.");
    }
}

// What unsubscribing turned off, for the page the email's link opens.
public record Unsubscribed(string Category, string Description);

// The signed-in user's notifications (the list in the header) and which of them are emailed.
public static class NotificationEndpoints
{
    private const int ListLength = 30;

    [Authorize]
    [WolverineGet("/api/notifications")]
    public static async Task<NotificationList> List(ClaimsPrincipal user, INotificationStore store, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var notifications = await store.ListAsync(userId, ListLength, cancellationToken);
        return new NotificationList([.. notifications.Select(NotificationSummary.Of)], await store.UnreadCountAsync(userId, cancellationToken));
    }

    [Authorize]
    [WolverinePost("/api/notifications/{id}/read")]
    public static async Task<NoContent> MarkRead(Guid id, ClaimsPrincipal user, INotificationStore store, TimeProvider clock, CancellationToken cancellationToken)
    {
        await store.MarkReadAsync(user.GetUserId(), id, clock.GetUtcNow(), cancellationToken);
        return TypedResults.NoContent();
    }

    [Authorize]
    [WolverinePost("/api/notifications/read-all")]
    public static async Task<NoContent> MarkAllRead(ClaimsPrincipal user, INotificationStore store, TimeProvider clock, CancellationToken cancellationToken)
    {
        await store.MarkReadAsync(user.GetUserId(), null, clock.GetUtcNow(), cancellationToken);
        return TypedResults.NoContent();
    }

    [Authorize]
    [WolverineGet("/api/account/email-preferences")]
    public static async Task<EmailPreferences> Preferences(ClaimsPrincipal user, INotificationStore store, CancellationToken cancellationToken)
        => Describe(await store.PreferencesAsync(user.GetUserId(), cancellationToken));

    [Authorize]
    [WolverinePut("/api/account/email-preferences")]
    public static async Task<EmailPreferences> UpdatePreferences(
        UpdateEmailPreferencesRequest request, ClaimsPrincipal user, INotificationStore store, CancellationToken cancellationToken)
    {
        var preferences = await store.PreferencesAsync(user.GetUserId(), cancellationToken);
        foreach (var choice in request.Categories)
        {
            preferences.EmailOff.Remove(choice.Category);
            if (!choice.Email)
            {
                preferences.EmailOff.Add(choice.Category);
            }
        }

        await store.SavePreferencesAsync(preferences, cancellationToken);
        return Describe(preferences);
    }

    // The link in a notification email: needs no login. Mail apps' "Unsubscribe" button POSTs to
    // it (RFC 8058, with a form body this ignores), and so does the /unsubscribe page.
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/notifications/unsubscribe")]
    public static async Task<Results<Ok<Unsubscribed>, NotFound>> Unsubscribe(
        [FromQuery] string? token, UnsubscribeTokens tokens, INotificationStore store, CancellationToken cancellationToken)
    {
        if (tokens.Read(token ?? string.Empty) is not { } read
            || !NotificationCategories.Descriptions.TryGetValue(read.Category, out var description))
        {
            return TypedResults.NotFound();
        }

        var preferences = await store.PreferencesAsync(read.UserId, cancellationToken);
        if (!preferences.EmailOff.Contains(read.Category, StringComparer.Ordinal))
        {
            preferences.EmailOff.Add(read.Category);
            await store.SavePreferencesAsync(preferences, cancellationToken);
        }

        return TypedResults.Ok(new Unsubscribed(read.Category, description));
    }

    private static EmailPreferences Describe(NotificationPreferences preferences)
        => new([.. NotificationCategories.Descriptions.Select(c => new EmailPreference(c.Key, c.Value, !preferences.EmailOff.Contains(c.Key, StringComparer.Ordinal)))]);
}
