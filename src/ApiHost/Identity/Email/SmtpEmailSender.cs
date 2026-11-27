using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ApiHost.Identity.Email;

public class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    // Auto (the default) uses TLS when the server offers it; None for a local test server such as
    // Mailpit; SslOnConnect for port 465; StartTls to require TLS on port 587.
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;
    public string? UserName { get; set; }
    public string? Password { get; set; }

    // The sender, as an address or "Name <address>".
    public string From { get; set; } = string.Empty;
}

// Sends email through the SMTP server configured under Email:Smtp, with MailKit. Most email
// services (Amazon SES, Mailgun, Postmark, SendGrid, ...) offer SMTP; or replace this class with
// their API client.
public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value;

        using var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(smtp.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.TextBody };
        if (message.UnsubscribeUrl is { } unsubscribe)
        {
            mime.Headers.Add("List-Unsubscribe", $"<{unsubscribe}>");
            mime.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, smtp.Security, cancellationToken);
        if (!string.IsNullOrEmpty(smtp.UserName))
        {
            await client.AuthenticateAsync(smtp.UserName, smtp.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(new SendRequest(mime), cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
