namespace ApiHost.Identity.Email;

// Used when no SMTP server is configured (Email:Smtp:Host): nothing can be sent, so log a warning
// with the subject only. Never the body, which holds one-time links that would let anyone with
// log access confirm or reset someone else's account.
public class NotConfiguredEmailSender(ILogger<NotConfiguredEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning("No email server is configured (set Email:Smtp:Host), so the email '{Subject}' was not sent", message.Subject);
        return Task.CompletedTask;
    }
}
