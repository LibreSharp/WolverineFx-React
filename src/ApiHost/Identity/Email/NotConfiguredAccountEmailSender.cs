namespace ApiHost.Identity.Email;

// Used outside Development when no SMTP server is configured: nothing can be sent, so log a
// warning (without the link, which would let anyone with log access reset the password).
public class NotConfiguredAccountEmailSender(ILogger<NotConfiguredAccountEmailSender> logger) : IAccountEmailSender
{
    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken)
    {
        logger.LogWarning("A password reset was requested but no email server is configured (set Email:Smtp:Host); nothing was sent");
        return Task.CompletedTask;
    }
}
