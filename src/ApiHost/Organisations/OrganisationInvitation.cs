namespace ApiHost.Organisations;

// An invitation to join an organisation, sent by email. The link carries a random token; only
// its hash is stored, set when the email is sent (OrganisationInvitationEmailHandler), so a
// leaked database can't be used to join. The invitation is deleted once accepted.
public class OrganisationInvitation
{
    public Guid Id { get; set; }

    public Guid OrganisationId { get; set; }

    public string Email { get; set; } = string.Empty;

    // Upper case, for matching the signed-in user's email (as Identity's NormalizedEmail is).
    public string NormalizedEmail { get; set; } = string.Empty;

    public string Role { get; set; } = OrganisationRoles.Member;

    public string InvitedByUserId { get; set; } = string.Empty;

    public string? TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
}

// A pending invitation, as owners and admins see it.
public record PendingInvitation(Guid Id, string Email, string Role, DateTimeOffset ExpiresAt);
