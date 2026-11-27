using System.Security.Claims;
using ApiHost.Identity;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
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
// an owner. Each user can send OrganisationOptions.InvitationsPerHour, and an organisation can
// have MaxPendingInvitations waiting. Accepting is in JoinOrganisationEndpoints.
public static class InvitationEndpoints
{
    [Authorize]
    [EnableRateLimiting(OrganisationSetup.InvitationsPolicy)]
    [WolverinePost("/api/organisation/invitations")]
    public static async Task<Results<Accepted, ValidationProblem>> Invite(
        InviteMemberRequest request,
        ClaimsPrincipal principal,
        IOrganisationStore store,
        UserManager<ApplicationUser> userManager,
        IOptions<OrganisationOptions> options,
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
            return IdentityResultExtensions.ValidationProblem("Role", refusal);
        }

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null && await store.FindMembershipAsync(organisationId, existing.Id, cancellationToken) is not null)
        {
            return IdentityResultExtensions.ValidationProblem("Email", "They're already a member.");
        }

        // A new invitation to someone already invited replaces theirs, so it doesn't count.
        var pending = (await store.InvitationsAsync(organisationId, cancellationToken))
            .Count(i => !string.Equals(i.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase));
        if (pending >= options.Value.MaxPendingInvitations)
        {
            return IdentityResultExtensions.ValidationProblem("Email",
                $"This organisation already has {pending} invitations waiting: revoke some, or wait for them to be accepted or expire.");
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
        var inviter = await userManager.FindByIdAsync(actor.UserId);
        var invitedBy = inviter?.DisplayName ?? inviter?.Email ?? "Someone";
        await store.AddInvitationAsync(invitation, new SendOrganisationInvitation(invitation.Id, invitedBy, ClientUrl.For(configuration, httpContext)), cancellationToken);
        return TypedResults.Accepted((string?)null);
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
