namespace ApiHost.Identity;

// The "Accounts" configuration section.
public class AccountOptions
{
    public const string SectionName = "Accounts";

    // When false, only people an admin invites can get an account (the admin page's Invite form).
    public bool AllowRegistration { get; set; } = true;

    // Accounts with these emails are made admins once their email is confirmed (at startup, and
    // when they log in). Set at least one in production, e.g. Accounts__AdminEmails__0. Removing an
    // email here doesn't take the role away; do that on the admin page.
    public IList<string> AdminEmails { get; set; } = [];

    public bool IsAdminEmail(string? email)
        => email is not null && AdminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
}
