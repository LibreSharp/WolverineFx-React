namespace ApiHost.Organisations;

// A team that owns data. Every Todo belongs to an organisation: Wolverine's multi-tenancy, with
// the organisation's id as the tenant id. A user can belong to several, and works in one at a
// time: the one in their access token (OrganisationClaims).
public class Organisation
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

// A user's place in an organisation.
public class Membership
{
    public Guid Id { get; set; }

    public Guid OrganisationId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Role { get; set; } = OrganisationRoles.Member;

    public DateTimeOffset JoinedAt { get; set; }

    // When the user last started working in this organisation: a new session starts there.
    public DateTimeOffset LastUsedAt { get; set; }
}

// Owner: everything, including renaming or deleting the organisation and choosing its owners.
// Admin: invites and removes members, and changes their roles, except owners'.
// Member: uses the app.
// There is always at least one owner.
public static class OrganisationRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";

    public static readonly IReadOnlyList<string> All = [Owner, Admin, Member];

    public static bool CanManageMembers(string role) => role is Owner or Admin;
}

// An organisation as one of the signed-in user's: IsCurrent for the one their token is for.
public record UserOrganisation(Guid Id, string Name, string Role, bool IsCurrent);

public record OrganisationMember(string UserId, string Email, string? DisplayName, string Role, DateTimeOffset JoinedAt);

public static class OrganisationNames
{
    public const int MaxLength = 100;
}
