using System.Net;
using System.Text.Json;
using Alba;
using ApiHost.Features.Auth;
using ApiHost.Features.Notifications;
using ApiHost.Features.Todos;
using ApiHost.Identity;
using ApiHost.Notifications;
using Microsoft.IdentityModel.JsonWebTokens;
using Wolverine.Tracking;

namespace ApiHost.Tests;

// With accounts (excluded otherwise by template.json): notifications, email preferences with
// one-click unsubscribe, and "Download your data".
[Collection("api")]
public class NotificationTests(AppFixture fixture)
{
    private const string Password = "P@ssword123!";

    private sealed record User(string Id, string Email, string AccessToken);

    private async Task<User> SignUpAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await fixture.RegisterConfirmedUserAsync(email, Password);
        var tokens = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, Password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LoginResponse>()!;
        return new User(new JsonWebToken(tokens.AccessToken).Subject, email, tokens.AccessToken);
    }

    // Waits for the notification, and its email, to be handled.
    private Task NotifyAsync(User user, string title)
        => fixture.Host.ExecuteAndWaitAsync(context => context.PublishAsync(
            new SendNotification(user.Id, NotificationCategories.Reminders, title, "Details.", "/todos")).AsTask());

    private async Task<NotificationList> ListAsync(User user)
        => (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/notifications");
            s.WithBearerToken(user.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<NotificationList>()!;

    private async Task<EmailPreferences> PreferencesAsync(User user)
        => (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/account/email-preferences");
            s.WithBearerToken(user.AccessToken);
        })).ReadAsJson<EmailPreferences>()!;

    [Fact]
    public async Task a_notification_is_listed_emailed_and_can_be_marked_read()
    {
        var user = await SignUpAsync();
        await NotifyAsync(user, "Reminder: water the plants");

        var list = await ListAsync(user);
        Assert.Equal(1, list.Unread);
        var notification = Assert.Single(list.Notifications);
        Assert.Equal(("Reminder: water the plants", "/todos", false), (notification.Title, notification.Link, notification.Read));

        // Emailed, with an unsubscribe link in the text and the one-click header.
        var email = fixture.Emails.SentTo(user.Email).Single(m => m.Subject == "Reminder: water the plants");
        Assert.Contains("/unsubscribe?token=", email.TextBody, StringComparison.Ordinal);
        Assert.Contains("/api/notifications/unsubscribe?token=", email.UnsubscribeUrl, StringComparison.Ordinal);

        await fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/notifications/{notification.Id}/read");
            s.WithBearerToken(user.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        Assert.Equal(0, (await ListAsync(user)).Unread);

        // Someone else's notifications stay theirs.
        var other = await SignUpAsync();
        Assert.Empty((await ListAsync(other)).Notifications);
    }

    [Fact]
    public async Task turning_a_category_off_stops_its_emails_but_not_the_notifications()
    {
        var user = await SignUpAsync();
        Assert.All((await PreferencesAsync(user)).Categories, c => Assert.True(c.Email));

        var updated = (await fixture.Host.Scenario(s =>
        {
            s.Put.Json(new UpdateEmailPreferencesRequest([new EmailPreference(NotificationCategories.Reminders, string.Empty, false)])).ToUrl("/api/account/email-preferences");
            s.WithBearerToken(user.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<EmailPreferences>()!;
        Assert.False(updated.Categories.Single(c => c.Category == NotificationCategories.Reminders).Email);

        await NotifyAsync(user, "Reminder: not emailed");
        Assert.Single((await ListAsync(user)).Notifications);
        Assert.DoesNotContain(fixture.Emails.SentTo(user.Email), m => m.Subject == "Reminder: not emailed");
    }

    [Fact]
    public async Task the_unsubscribe_link_works_without_logging_in_and_cannot_be_forged()
    {
        var user = await SignUpAsync();
        await NotifyAsync(user, "Reminder: unsubscribe me");
        var link = fixture.Emails.SentTo(user.Email).Single(m => m.Subject == "Reminder: unsubscribe me").UnsubscribeUrl!;

        // What a mail app's "Unsubscribe" button sends (RFC 8058): a form POST to the link.
        using var client = fixture.Host.Server.CreateClient();
        var response = await client.PostAsync(new Uri(link).PathAndQuery, new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal) { ["List-Unsubscribe"] = "One-Click" }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var unsubscribed = JsonSerializer.Deserialize<Unsubscribed>(await response.Content.ReadAsStringAsync(), JsonSerializerOptions.Web)!;
        Assert.Equal(NotificationCategories.Reminders, unsubscribed.Category);
        Assert.False((await PreferencesAsync(user)).Categories.Single(c => c.Category == NotificationCategories.Reminders).Email);

        await fixture.Host.Scenario(s =>
        {
            s.Post.Url("/api/notifications/unsubscribe?token=made-up");
            s.StatusCodeShouldBe(HttpStatusCode.NotFound);
        });
    }

    [Fact]
    public async Task download_your_data_has_the_account_and_its_notifications_but_no_secrets()
    {
        var user = await SignUpAsync();
        await NotifyAsync(user, "Reminder: in the export");
        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateTodo("Exported Todo")).ToUrl("/api/todos");
            s.WithBearerToken(user.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });

        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/account/export");
            s.WithBearerToken(user.AccessToken);
            s.StatusCodeShouldBeOk();
        });
        Assert.Contains("attachment", result.Context.Response.Headers.ContentDisposition.ToString(), StringComparison.Ordinal);
        var text = result.ReadAsText();
        Assert.DoesNotContain("PasswordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", text, StringComparison.OrdinalIgnoreCase);

        using var file = JsonDocument.Parse(text);
        Assert.Equal(user.Email, file.RootElement.GetProperty("account").GetProperty("email").GetString());
        Assert.Contains("Reminder: in the export", file.RootElement.GetProperty("notifications").ToString(), StringComparison.Ordinal);
#if (Saas)
        // A saas user's Todos belong to their organisation: they're in its download instead.
        Assert.True(file.RootElement.TryGetProperty("organisations", out _));
#else
        Assert.Contains("Exported Todo", file.RootElement.GetProperty("todos").ToString(), StringComparison.Ordinal);
#endif
    }
}
