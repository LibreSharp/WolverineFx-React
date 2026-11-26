namespace ApiHost.Identity.Email;

// Sends account emails. IdentityJwtSetup picks the implementation:
// - SMTP, when Email:Smtp:Host is configured (any environment);
// - otherwise in Development, LoggingAccountEmailSender, which writes the email to the log;
// - otherwise NotConfiguredAccountEmailSender, which only logs a warning (never the link).
public interface IAccountEmailSender
{
    Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken);
}
