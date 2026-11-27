using ApiHost.Organisations;

namespace ApiHost.Features.Organisations;

// Who may change whom in an organisation (see OrganisationRoles). Each method returns why the
// change isn't allowed, or null if it is. ownerCount: the organisation's owners now.
public static class OrganisationRules
{
    public const string NeedsAnOwner = "An organisation needs an owner: make someone else an owner first.";

    public static string? CanChangeRole(Membership actor, Membership target, string newRole, int ownerCount)
    {
        if (!OrganisationRoles.CanManageMembers(actor.Role))
        {
            return "Only owners and admins can change roles.";
        }

        if ((target.Role == OrganisationRoles.Owner || newRole == OrganisationRoles.Owner) && actor.Role != OrganisationRoles.Owner)
        {
            return "Only owners can make someone an owner, or change an owner's role.";
        }

        return LeavesNoOwner(target, ownerCount) && newRole != OrganisationRoles.Owner
            ? NeedsAnOwner
            : null;
    }

    // Removing a member, or leaving (actor and target are the same).
    public static string? CanRemove(Membership actor, Membership target, int ownerCount)
    {
        var leaving = actor.UserId == target.UserId;
        if (!leaving && !OrganisationRoles.CanManageMembers(actor.Role))
        {
            return "Only owners and admins can remove members.";
        }

        if (!leaving && target.Role == OrganisationRoles.Owner && actor.Role != OrganisationRoles.Owner)
        {
            return "Only owners can remove an owner.";
        }

        return LeavesNoOwner(target, ownerCount)
            ? "An organisation needs an owner: make someone else an owner first, or delete the organisation."
            : null;
    }

    private static bool LeavesNoOwner(Membership target, int ownerCount)
        => target.Role == OrganisationRoles.Owner && ownerCount <= 1;
}
