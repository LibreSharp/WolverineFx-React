namespace ApiHost.Organisations;

// Limits on invitations, which send email from the app's domain to any address, with an
// organisation name the sender chooses: without them, anyone could sign up and use the app to
// send spam or phishing. Set them in the "Organisations" section, e.g. Organisations__InvitationsPerHour.
public class OrganisationOptions
{
    public const string SectionName = "Organisations";

    // Invitations each user can send an hour, across their organisations; after that they get
    // 429 Too Many Requests.
    public int InvitationsPerHour { get; set; } = 20;

    // Unexpired invitations an organisation can have waiting at once.
    public int MaxPendingInvitations { get; set; } = 100;
}
