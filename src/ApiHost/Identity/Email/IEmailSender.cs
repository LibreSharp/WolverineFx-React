namespace ApiHost.Identity.Email;

// UnsubscribeUrl: for emails people can turn off (notifications), the address that turns them off
// with one POST (RFC 8058); mail apps show it as an "Unsubscribe" button. Not for account emails.
public record EmailMessage(string To, string Subject, string TextBody, string? UnsubscribeUrl = null);

// Sends an email. IdentityJwtSetup picks the implementation: SmtpEmailSender when Email:Smtp:Host
// is configured (Development points it at Mailpit, see docker-compose.yml), otherwise
// NotConfiguredEmailSender, which only logs a warning.
//
// Don't call this from an endpoint: publish a message instead (see AccountEmails.cs), so the
// email goes through Wolverine's durable outbox and is retried if the mail server is down.
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
