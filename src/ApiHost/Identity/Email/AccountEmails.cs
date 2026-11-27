using JasperFx.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ApiHost.Identity.Email;

// Requests for account emails. Endpoints return them as cascaded messages, and Wolverine keeps
// them in its durable inbox (UseDurableLocalQueues in Program.cs), so an email isn't lost if the
// app restarts or the mail server is down. ClientUrl is where the React app is served (see
// ClientUrl.cs); the links in the emails open its pages.
public record SendEmailConfirmation(string UserId, string ClientUrl);

public record SendPasswordReset(string UserId, string ClientUrl);

public record SendEmailChangeConfirmation(string UserId, string NewEmail, string ClientUrl);

// Sends the emails. Each handler creates the one-time token itself, when it runs, so no token is
// ever stored in the message tables. Tokens are checked by the endpoints the links lead to
// (ConfirmEmailEndpoint, ResetPasswordEndpoint, ConfirmEmailChangeEndpoint).
// Wolverine finds handlers by name: the class name must end in "Handler".
public static class AccountEmailHandler
{
    // When sending fails (for example, the mail server is unreachable), try again later. After
    // the last retry, Wolverine moves the message to its dead letter queue (wolverine_dead_letters).
    public static void Configure(HandlerChain chain)
    {
        chain.OnAnyException().ScheduleRetry(30.Seconds(), 2.Minutes(), 10.Minutes(), 1.Hours());
    }

    public static async Task Handle(
        SendEmailConfirmation message, UserManager<ApplicationUser> userManager, IEmailSender emailSender, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(message.UserId);
        if (user?.Email is null || user.EmailConfirmed)
        {
            return;
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = Link(message.ClientUrl, "confirm-email", ("userId", user.Id), ("token", token));
        await emailSender.SendAsync(new EmailMessage(user.Email, "Confirm your email address", $"""
            Welcome! To confirm your email address and start using your account, open:

            {link}

            The link expires in a day. If you didn't create an account, you can ignore this email.
            """), cancellationToken);
    }

    public static async Task Handle(
        SendPasswordReset message, UserManager<ApplicationUser> userManager, IEmailSender emailSender, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(message.UserId);
        if (user?.Email is null)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = Link(message.ClientUrl, "reset-password", ("email", user.Email), ("token", token));
        await emailSender.SendAsync(new EmailMessage(user.Email, "Reset your password", $"""
            Someone asked to reset the password for this account. To choose a new password, open:

            {link}

            The link expires in an hour. If you didn't ask for this, you can ignore this email.
            """), cancellationToken);
    }

    // Sent to the new address: the email changes only when its owner opens the link.
    public static async Task Handle(
        SendEmailChangeConfirmation message, UserManager<ApplicationUser> userManager, IEmailSender emailSender, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(message.UserId);
        if (user is null)
        {
            return;
        }

        var token = await userManager.GenerateChangeEmailTokenAsync(user, message.NewEmail);
        var link = Link(message.ClientUrl, "confirm-email-change", ("userId", user.Id), ("email", message.NewEmail), ("token", token));
        await emailSender.SendAsync(new EmailMessage(message.NewEmail, "Confirm your new email address", $"""
            To use this email address for your account, open:

            {link}

            The link expires in a day. If you didn't ask for this, you can ignore this email.
            """), cancellationToken);
    }

    private static string Link(string clientUrl, string page, params (string Name, string Value)[] query)
        => QueryHelpers.AddQueryString($"{clientUrl}/{page}",
            query.ToDictionary(q => q.Name, q => (string?)q.Value, StringComparer.Ordinal));
}
