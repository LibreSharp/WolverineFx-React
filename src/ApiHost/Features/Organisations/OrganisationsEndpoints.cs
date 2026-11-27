using System.Security.Claims;
using ApiHost.Identity;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine.Http;

namespace ApiHost.Features.Organisations;

public record CreateOrganisationRequest(string Name);

public class CreateOrganisationRequestValidator : AbstractValidator<CreateOrganisationRequest>
{
    public CreateOrganisationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(OrganisationNames.MaxLength);
    }
}

public record OrganisationCreationResponse : CreationResponse
{
    public OrganisationCreationResponse(Guid id) : base("/api/organisations")
    {
        Id = id;
    }

    public Guid Id { get; }
}

// RefreshToken: the session's, which is replaced by one for the new organisation.
public record SwitchOrganisationRequest(Guid OrganisationId, string RefreshToken);

// The signed-in user's organisations: listing them, creating one, and switching between them.
public static class OrganisationsEndpoints
{
    [Authorize]
    [WolverineGet("/api/organisations")]
    public static Task<IReadOnlyList<UserOrganisation>> Get(ClaimsPrincipal user, IOrganisationStore store, CancellationToken cancellationToken)
        => store.OrganisationsOfUserAsync(user.GetUserId(), user.GetOrganisationId(), cancellationToken);

    // The creator becomes its owner. Switch to it to work in it.
    [Authorize]
    [WolverinePost("/api/organisations")]
    public static async Task<OrganisationCreationResponse> Post(
        CreateOrganisationRequest request, ClaimsPrincipal user, OrganisationAccess organisations, CancellationToken cancellationToken)
    {
        var organisation = await organisations.CreateAsync(request.Name, user.GetUserId(), cancellationToken);
        return new OrganisationCreationResponse(organisation.Id);
    }

    // New tokens for the same session, working in another of the user's organisations. The old
    // refresh token is revoked, as when refreshing.
    [Authorize]
    [WolverinePost("/api/organisations/switch")]
    public static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ForbidHttpResult>> Switch(
        SwitchOrganisationRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenStore refreshTokenStore,
        IOrganisationStore store,
        OrganisationAccess organisations,
        TokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var session = await refreshTokenStore.FindByHashAsync(JwtTokenService.HashToken(request.RefreshToken), cancellationToken);
        if (session is null || session.UserId != userId || !session.IsActive || session.ReplacedByTokenHash is not null)
        {
            return TypedResults.Unauthorized();
        }

        var membership = await store.FindMembershipAsync(request.OrganisationId, userId, cancellationToken);
        var user = await userManager.FindByIdAsync(userId);
        if (membership is null || user is null || (await store.FindAsync(request.OrganisationId, cancellationToken))?.SuspendedAt is not null)
        {
            return TypedResults.Forbid();
        }

        await organisations.MarkUsedAsync(membership, cancellationToken);
        return TypedResults.Ok(await tokenIssuer.IssueAsync(user, session, membership.OrganisationId, cancellationToken));
    }
}
