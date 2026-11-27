namespace ApiHost.Organisations;

// Organisations and their members, in Marten or EF Core (OrganisationStore). Each change is
// saved straight away.
public interface IOrganisationStore
{
    Task<Organisation?> FindAsync(Guid organisationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Membership>> MembershipsOfUserAsync(string userId, CancellationToken cancellationToken);

    // The user's organisations, by name; isCurrent: the one their token is for.
    Task<IReadOnlyList<UserOrganisation>> OrganisationsOfUserAsync(string userId, Guid? current, CancellationToken cancellationToken);

    Task<Membership?> FindMembershipAsync(Guid organisationId, string userId, CancellationToken cancellationToken);

    // Whether the user is a member of the organisation and it isn't suspended: checked on every
    // request (OrganisationSetup), in one query.
    Task<bool> CanWorkInAsync(Guid organisationId, string userId, CancellationToken cancellationToken);

    // Every membership of the organisation, including any whose account was just deleted.
    Task<IReadOnlyList<Membership>> MembershipsOfOrganisationAsync(Guid organisationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganisationMember>> MembersAsync(Guid organisationId, CancellationToken cancellationToken);

    Task<int> CountOwnersAsync(Guid organisationId, CancellationToken cancellationToken);

    Task CreateAsync(Organisation organisation, Membership owner, CancellationToken cancellationToken);

    Task SaveAsync(Organisation organisation, CancellationToken cancellationToken);

    Task AddMembershipAsync(Membership membership, CancellationToken cancellationToken);

    Task SaveMembershipAsync(Membership membership, CancellationToken cancellationToken);

    Task RemoveMembershipAsync(Membership membership, CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingInvitation>> InvitationsAsync(Guid organisationId, CancellationToken cancellationToken);

    Task<OrganisationInvitation?> FindInvitationAsync(Guid invitationId, CancellationToken cancellationToken);

    Task<OrganisationInvitation?> FindInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    // Adds the invitation, replacing any earlier one to the same email for the organisation, and
    // publishes the message that emails it in the same transaction (Wolverine's outbox), so an
    // invitation is never saved without its email being sent.
    Task AddInvitationAsync(OrganisationInvitation invitation, SendOrganisationInvitation email, CancellationToken cancellationToken);

    Task SaveInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken);

    Task RemoveInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken);

    // Deletes the organisation, its memberships and its invitations, and publishes OrganisationDeleted
    // (for the organisation's tenant) in the same transaction, through Wolverine's outbox: the
    // features that own its data (Todos) delete it when they handle the message. Both happen or
    // neither does, so a crash can't leave data behind for an organisation that no longer exists.
    Task DeleteAsync(Guid organisationId, CancellationToken cancellationToken);
}
