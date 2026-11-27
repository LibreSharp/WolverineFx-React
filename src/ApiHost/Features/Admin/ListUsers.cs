using ApiHost.Identity;
namespace ApiHost.Features.Admin;

// InvitationPending: invited by an admin, and hasn't chosen a password yet.
// LockedUntil: set while the account is locked out; LockedByAdmin when an admin locked it.
public record AdminUser(
    string Id,
    string Email,
    string? DisplayName,
    bool EmailConfirmed,
    bool InvitationPending,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LockedUntil,
    bool LockedByAdmin);

// AllRoles: the roles an admin can assign (AppRoles.All).
public record AdminUserList(IReadOnlyList<AdminUser> Users, int Total, int Page, int PageSize, IReadOnlyList<string> AllRoles);

public static class AdminUsers
{
    public const int PageSize = 25;

    public static AdminUser ToAdminUser(ApplicationUser user, IReadOnlyList<string> roles) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.DisplayName,
        user.EmailConfirmed,
        InvitationPending: user.PasswordHash is null && !user.EmailConfirmed,
        roles,
        LockedUntil: user.LockoutEnd > DateTimeOffset.UtcNow ? user.LockoutEnd : null,
        LockedByAdmin: user.IsLockedByAdmin());
}
