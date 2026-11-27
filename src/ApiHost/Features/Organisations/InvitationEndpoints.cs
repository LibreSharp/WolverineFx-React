using System.Security.Claims;
using ApiHost.Identity;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Organisations;

public record InviteMemberRequest(string Email, string Role);

public class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).Must(role => OrganisationRoles.All.Contains(role, StringComparer.Ordinal))
            .WithMessage($"Unknown role. Roles: {string.Join(", ", OrganisationRoles.All)}.");
    }
}

// Owners and admins invite people to the current organisation by email; only owners can invite
// an owner. Accepting is in JoinOrganisationEndpoints.
public static class InvitationEndpoints
{
    [Authorize]
    [WolverinePost("/api/organisation/invitations")]
    public static async Task<(Results<Accepted, ValidationProblem>, OutgoingMessages)> Invite(
        InviteMemberRequest request,
        ClaimsPrincipal principal,
        IOrganisationStore store,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var organisationId = principal.GetOrganisationId();
        var actor = await store.FindMembershipAsync(organisationId, principal.GetUserId(), cancellationToken);
        var refusal = actor is null || !OrganisationRoles.CanManageMembers(actor.Role)
            ? "Only owners and admins can invite people."
            : request.Role == OrganisationRoles.Owner && actor.Role != OrganisationRoles.Owner
                ? "Only owners can invite an owner."
                : null;
        if (refusal is not null)
        {
            return (IdentityResultExtensions.ValidationProblem("Role", refusal), []);
        }

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null && await store.FindMembershipAsync(organisationId, existing.Id, cancellationToken) is not null)
        {
            return (IdentityResultExtensions.ValidationProblem("Email", "They're already a member."), []);
        }

        var now = clock.GetUtcNow();
        var invitation = new OrganisationInvitation
        {
            Id = Guid.CreateVersion7(),
            OrganisationId = organisationId,
            Email = request.Email.Trim(),
            NormalizedEmail = userManager.NormalizeEmail(request.Email.Trim()),
            Role = request.Role,
            InvitedByUserId = actor!.UserId,
            CreatedAt = now,
            ExpiresAt = now + OrganisationInvitation.Lifetime,
        };
        await store.AddInvitationAsync(invitation, cancellationToken);

        var inviter = await userManager.FindByIdAsync(actor.UserId);
        var invitedBy = inviter?.DisplayName ?? inviter?.Email ?? "Someone";
        return (TypedResults.Accepted((string?)null),
            [new SendOrganisationInvitation(invitation.Id, invitedBy, ClientUrl.For(configuration, httpContext))]);
    }

    [Authorize]
    [WolverineGet("/api/organisation/invitations")]
    public static async Task<Results<Ok<IReadOnlyList<PendingInvitation>>, ForbidHttpResult>> List(
        ClaimsPrincipal principal, IOrganisationStore store, CancellationToken cancellationToken)
    {
        var organisationId = principal.GetOrganisationId();
        var actor = await store.FindMembershipAsync(organisationId, principal.GetUserId(), cancellationToken);
        return actor is null || !OrganisationRoles.CanManageMembers(actor.Role)
            ? TypedResults.Forbid()
            : TypedResults.Ok(await store.InvitationsAsync(organisationId, cancellationToken));
    }

    [Authorize]
    [WolverineDelete("/api/organisation/invitations/{id}")]
    public static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Revoke(
        Guid id, ClaimsPrincipal principal, IOrganisationStore store, CancellationToken cancellationToken)
    {
        var organisationId = principal.GetOrganisationId();
        var actor = await store.FindMembershipAsync(organisationId, principal.GetUserId(), cancellationToken);
        if (actor is null || !OrganisationRoles.CanManageMembers(actor.Role))
        {
            return TypedResults.Forbid();
        }

        var invitation = await store.FindInvitationAsync(id, cancellationToken);
        if (invitation is null || invitation.OrganisationId != organisationId)
        {
            return TypedResults.NotFound();
        }

        await store.RemoveInvitationAsync(invitation, cancellationToken);
        return TypedResults.NoContent();
    }
}
