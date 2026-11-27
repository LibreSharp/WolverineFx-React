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

    // Adds the invitation, replacing any earlier one to the same email for the organisation.
    Task AddInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken);

    Task SaveInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken);

    Task RemoveInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken);

    // The organisation, its memberships and its invitations. Its data (Todos) is deleted by the features that own
    // it, which handle the OrganisationDeleted message.
    Task DeleteAsync(Guid organisationId, CancellationToken cancellationToken);
}
