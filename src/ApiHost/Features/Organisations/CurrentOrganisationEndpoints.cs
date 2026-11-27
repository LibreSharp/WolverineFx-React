using System.Security.Claims;
using ApiHost.Identity;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace ApiHost.Features.Organisations;

// The organisation the user is working in, and their role in it.
public record CurrentOrganisation(Guid Id, string Name, string Role);

public record RenameOrganisationRequest(string Name);

public class RenameOrganisationRequestValidator : AbstractValidator<RenameOrganisationRequest>
{
    public RenameOrganisationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(OrganisationNames.MaxLength);
    }
}

public record ChangeMemberRoleRequest(string Role);

public class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role).Must(role => OrganisationRoles.All.Contains(role, StringComparer.Ordinal))
            .WithMessage($"Unknown role. Roles: {string.Join(", ", OrganisationRoles.All)}.");
    }
}

// The current organisation (the one in the access token): its name, its members and their
// roles. Rules on who may do what are in OrganisationRules.
public static class CurrentOrganisationEndpoints
{
    [Authorize]
    [WolverineGet("/api/organisation")]
    public static async Task<Results<Ok<CurrentOrganisation>, NotFound>> Get(
        ClaimsPrincipal user, IOrganisationStore store, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        var organisation = await store.FindAsync(organisationId, cancellationToken);
        var membership = await store.FindMembershipAsync(organisationId, user.GetUserId(), cancellationToken);
        return organisation is null || membership is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new CurrentOrganisation(organisation.Id, organisation.Name, membership.Role));
    }

    [Authorize]
    [WolverinePut("/api/organisation")]
    public static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Rename(
        RenameOrganisationRequest request, ClaimsPrincipal user, IOrganisationStore store, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await IsOwnerAsync(store, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        var organisation = await store.FindAsync(organisationId, cancellationToken);
        if (organisation is null)
        {
            return TypedResults.NotFound();
        }

        organisation.Name = request.Name.Trim();
        await store.SaveAsync(organisation, cancellationToken);
        return TypedResults.NoContent();
    }

    // Deletes the organisation for everyone. Its data is deleted in the background by each
    // feature's OrganisationDeleted handler, run for this organisation's tenant (the store
    // publishes the message in the same transaction as the deletion). Members' tokens stop
    // working at once; the browser then refreshes them into another organisation.
    [Authorize]
    [WolverineDelete("/api/organisation")]
    public static async Task<Results<NoContent, ForbidHttpResult>> Delete(
        ClaimsPrincipal user, IOrganisationStore store, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await IsOwnerAsync(store, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        await store.DeleteAsync(organisationId, cancellationToken);
        return TypedResults.NoContent();
    }

    [Authorize]
    [WolverineGet("/api/organisation/members")]
    public static Task<IReadOnlyList<OrganisationMember>> Members(
        ClaimsPrincipal user, IOrganisationStore store, CancellationToken cancellationToken)
        => store.MembersAsync(user.GetOrganisationId(), cancellationToken);

    [Authorize]
    [WolverinePut("/api/organisation/members/{userId}/role")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> ChangeRole(
        string userId, ChangeMemberRoleRequest request, ClaimsPrincipal user, IOrganisationStore store, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        var actor = await store.FindMembershipAsync(organisationId, user.GetUserId(), cancellationToken);
        var target = await store.FindMembershipAsync(organisationId, userId, cancellationToken);
        if (actor is null || target is null)
        {
            return TypedResults.NotFound();
        }

        var refusal = OrganisationRules.CanChangeRole(actor, target, request.Role, await store.CountOwnersAsync(organisationId, cancellationToken));
        if (refusal is not null)
        {
            return IdentityResultExtensions.ValidationProblem("Role", refusal);
        }

        var previousRole = target.Role;
        target.Role = request.Role;
        await store.SaveMembershipAsync(target, cancellationToken);

        // Another change at the same moment (two owners stepping down together) could have passed
        // the same check: if no owner is left now, undo this one.
        if (previousRole == OrganisationRoles.Owner && request.Role != OrganisationRoles.Owner
            && await store.CountOwnersAsync(organisationId, cancellationToken) == 0)
        {
            target.Role = previousRole;
            await store.SaveMembershipAsync(target, cancellationToken);
            return IdentityResultExtensions.ValidationProblem("Role", OrganisationRules.NeedsAnOwner);
        }

        return TypedResults.NoContent();
    }

    // Removes a member, or (for your own user id) leaves the organisation.
    [Authorize]
    [WolverineDelete("/api/organisation/members/{userId}")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> Remove(
        string userId, ClaimsPrincipal user, IOrganisationStore store, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        var actor = await store.FindMembershipAsync(organisationId, user.GetUserId(), cancellationToken);
        var target = await store.FindMembershipAsync(organisationId, userId, cancellationToken);
        if (actor is null || target is null)
        {
            return TypedResults.NotFound();
        }

        var refusal = OrganisationRules.CanRemove(actor, target, await store.CountOwnersAsync(organisationId, cancellationToken));
        if (refusal is not null)
        {
            return IdentityResultExtensions.ValidationProblem("UserId", refusal);
        }

        await store.RemoveMembershipAsync(target, cancellationToken);

        // As in ChangeRole: if a change at the same moment left no owner, put this one back.
        if (target.Role == OrganisationRoles.Owner && await store.CountOwnersAsync(organisationId, cancellationToken) == 0)
        {
            await store.AddMembershipAsync(target, cancellationToken);
            return IdentityResultExtensions.ValidationProblem("UserId", OrganisationRules.NeedsAnOwner);
        }

        return TypedResults.NoContent();
    }

    private static async Task<bool> IsOwnerAsync(IOrganisationStore store, Guid organisationId, ClaimsPrincipal user, CancellationToken cancellationToken)
        => (await store.FindMembershipAsync(organisationId, user.GetUserId(), cancellationToken))?.Role == OrganisationRoles.Owner;
}
