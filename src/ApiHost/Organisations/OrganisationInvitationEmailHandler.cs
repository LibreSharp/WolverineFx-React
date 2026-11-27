using System.Security.Cryptography;
using ApiHost.Identity;
using ApiHost.Identity.Email;
using JasperFx.Core;
using Microsoft.AspNetCore.WebUtilities;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ApiHost.Organisations;

// InvitedBy: the inviting member's name or email, shown in the email.
public record SendOrganisationInvitation(Guid InvitationId, string InvitedBy, string ClientUrl);

// Sends an invitation email. Like the account emails (Identity/Email/AccountEmails.cs), the
// token is created when the email is sent, so it's never in Wolverine's message tables; only its
// hash is stored, on the invitation. Sending again (a new invitation to the same email) gives a
// new token, and the old link stops working.
public static class OrganisationInvitationEmailHandler
{
    public static void Configure(HandlerChain chain)
    {
        chain.OnAnyException().ScheduleRetry(30.Seconds(), 2.Minutes(), 10.Minutes(), 1.Hours());
    }

    public static async Task Handle(
        SendOrganisationInvitation message, IOrganisationStore store, IEmailSender emailSender, CancellationToken cancellationToken)
    {
        var invitation = await store.FindInvitationAsync(message.InvitationId, cancellationToken);
        var organisation = invitation is null ? null : await store.FindAsync(invitation.OrganisationId, cancellationToken);
        if (invitation is null || organisation is null || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return;
        }

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        invitation.TokenHash = JwtTokenService.HashToken(token);
        await store.SaveInvitationAsync(invitation, cancellationToken);

        var link = QueryHelpers.AddQueryString($"{message.ClientUrl}/join", "token", token);
        await emailSender.SendAsync(new EmailMessage(invitation.Email, $"Join {organisation.Name}", $"""
            {message.InvitedBy} has invited you to join {organisation.Name}. To accept, open:

            {link}

            The link expires in a week. If you weren't expecting this, you can ignore this email.
            """), cancellationToken);
    }
}
