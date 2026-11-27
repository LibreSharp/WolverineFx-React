using ApiHost.Identity;
using ApiHost.Notifications;
using Wolverine;

namespace ApiHost.Organisations;

public class OrganisationAccess(IOrganisationStore store, IMessageBus bus, TimeProvider clock)
{
    // The organisation a new or refreshed session works in: the preferred one (the session's
    // current one, or the one being switched to) while the user is a member of it, otherwise the
    // one they used last. A user with none (a new account, or one removed from all of theirs)
    // gets their own.
    public async Task<Guid> ChooseAsync(ApplicationUser user, Guid? preferred, CancellationToken cancellationToken)
    {
        // Not a suspended one: the user can't work in it.
        var memberships = new List<Membership>();
        foreach (var membership in await store.MembershipsOfUserAsync(user.Id, cancellationToken))
        {
            if ((await store.FindAsync(membership.OrganisationId, cancellationToken))?.SuspendedAt is null)
            {
                memberships.Add(membership);
            }
        }

        var chosen = memberships.FirstOrDefault(m => m.OrganisationId == preferred)
            ?? memberships.MaxBy(m => m.LastUsedAt);
        if (chosen is not null)
        {
            return chosen.OrganisationId;
        }

        // Named after the user. An email can be longer than an organisation's name may be.
        var name = (user.DisplayName ?? user.Email ?? "My organisation").Trim();
        var personal = await CreateAsync(name.Length > OrganisationNames.MaxLength ? name[..OrganisationNames.MaxLength].TrimEnd() : name, user.Id, cancellationToken);
        return personal.Id;
    }

    // A new organisation, owned by the user who creates it.
    public async Task<Organisation> CreateAsync(string name, string ownerUserId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var organisation = new Organisation { Id = Guid.CreateVersion7(), Name = name.Trim(), CreatedAt = now };
        var owner = new Membership
        {
            Id = Guid.CreateVersion7(),
            OrganisationId = organisation.Id,
            UserId = ownerUserId,
            Role = OrganisationRoles.Owner,
            JoinedAt = now,
            LastUsedAt = now,
        };
        await store.CreateAsync(organisation, owner, cancellationToken);
        return organisation;
    }

    // The organisations that would be left without an owner if the user went: those where they
    // are the only owner and someone else is a member. (Ones where they're alone are deleted.)
    public async Task<IReadOnlyList<string>> NeedingAnotherOwnerAsync(string userId, CancellationToken cancellationToken)
    {
        var names = new List<string>();
        foreach (var membership in (await store.MembershipsOfUserAsync(userId, cancellationToken)).Where(m => m.Role == OrganisationRoles.Owner))
        {
            var memberships = await store.MembershipsOfOrganisationAsync(membership.OrganisationId, cancellationToken);
            if (memberships.Count(m => m.Role == OrganisationRoles.Owner) == 1 && memberships.Count > 1)
            {
                names.Add((await store.FindAsync(membership.OrganisationId, cancellationToken))?.Name ?? string.Empty);
            }
        }

        return names;
    }

    // Accepting an invitation: the user joins with the invited role (or keeps their place if
    // they're already a member), the organisation becomes the one they use next, and the
    // invitation is used up. Its owners and admins are notified.
    public async Task JoinAsync(OrganisationInvitation invitation, string userId, CancellationToken cancellationToken)
    {
        if (await store.FindMembershipAsync(invitation.OrganisationId, userId, cancellationToken) is null)
        {
            var now = clock.GetUtcNow();
            await store.AddMembershipAsync(new Membership
            {
                Id = Guid.CreateVersion7(),
                OrganisationId = invitation.OrganisationId,
                UserId = userId,
                Role = invitation.Role,
                JoinedAt = now,
                LastUsedAt = now,
            }, cancellationToken);
            await NotifyManagersAsync(invitation, userId, cancellationToken);
        }

        await store.RemoveInvitationAsync(invitation, cancellationToken);
    }

    private async Task NotifyManagersAsync(OrganisationInvitation invitation, string userId, CancellationToken cancellationToken)
    {
        var name = (await store.FindAsync(invitation.OrganisationId, cancellationToken))?.Name ?? "your organisation";
        var managers = (await store.MembershipsOfOrganisationAsync(invitation.OrganisationId, cancellationToken))
            .Where(m => m.UserId != userId && OrganisationRoles.CanManageMembers(m.Role));
        foreach (var manager in managers)
        {
            await bus.PublishAsync(new SendNotification(
                manager.UserId, NotificationCategories.Members, $"{invitation.Email} joined {name}", $"They joined as {invitation.Role}.", "/organisation"));
        }
    }

    // Switching to an organisation makes it the one a new session starts in.
    public async Task MarkUsedAsync(Membership membership, CancellationToken cancellationToken)
    {
        membership.LastUsedAt = clock.GetUtcNow();
        await store.SaveMembershipAsync(membership, cancellationToken);
    }
}
