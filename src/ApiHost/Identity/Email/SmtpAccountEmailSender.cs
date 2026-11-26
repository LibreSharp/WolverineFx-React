using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ApiHost.Identity.Email;

public class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = string.Empty;
}

// Sends email through an SMTP server configured under Email:Smtp. Most email services (SendGrid,
// Mailgun, Amazon SES, Postmark, ...) offer SMTP, or replace this class with their API client.
public class SmtpAccountEmailSender(IOptions<SmtpOptions> options) : IAccountEmailSender
{
    public async Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
        if (!string.IsNullOrEmpty(smtp.UserName))
        {
            client.Credentials = new NetworkCredential(smtp.UserName, smtp.Password);
        }

        using var message = new MailMessage(smtp.From, toEmail)
        {
            Subject = "Reset your password",
            Body = $"""
                Someone asked to reset the password for this account. To choose a new password, open:

                {resetLink}

                The link expires in an hour. If you didn't ask for this, you can ignore this email.
                """
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
