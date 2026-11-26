namespace ApiHost.Identity.Email;

// Development only: "sends" email by writing it to the API's log, so password reset works
// without a mail server. Never use outside Development: the log would contain reset links.
public class LoggingAccountEmailSender(ILogger<LoggingAccountEmailSender> logger) : IAccountEmailSender
{
    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken)
    {
        logger.LogInformation("Password reset email for {Email} (Development: not sent). Reset link: {ResetLink}", toEmail, resetLink);
        return Task.CompletedTask;
    }
}
