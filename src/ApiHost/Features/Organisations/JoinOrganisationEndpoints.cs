using System.Security.Claims;
using ApiHost.Identity;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine.Http;

namespace ApiHost.Features.Organisations;

// What the invitation link's page shows. AccountExists: log in to accept, rather than choosing a
// password.
public record InvitationDetails(string OrganisationName, string Email, string Role, bool AccountExists);

// The token goes in the body, not the URL, so it isn't recorded in traces or logs.
public record InvitationDetailsRequest(string Token);

public record JoinOrganisationRequest(string Token);

public record JoinOrganisationResponse(Guid OrganisationId);

public record JoinWithNewAccountRequest(string Token, string Password);

public class JoinWithNewAccountRequestValidator : AbstractValidator<JoinWithNewAccountRequest>
{
    public JoinWithNewAccountRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

// The invitation link (/join?token=...); the page sends the token to these endpoints in request bodies. A signed-in user whose email matches accepts it; someone
// without an account creates one from it: the link proves they own the email, so it's confirmed
// at once. Invitations work even when registration is closed (Accounts:AllowRegistration).
public static class JoinOrganisationEndpoints
{
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/join/details")]
    public static async Task<Results<Ok<InvitationDetails>, NotFound>> Details(
        InvitationDetailsRequest request, IOrganisationStore store, UserManager<ApplicationUser> userManager, TimeProvider clock, CancellationToken cancellationToken)
    {
        var invitation = await FindAsync(store, request.Token, clock, cancellationToken);
        var organisation = invitation is null ? null : await store.FindAsync(invitation.OrganisationId, cancellationToken);
        if (invitation is null || organisation is null)
        {
            return TypedResults.NotFound();
        }

        var accountExists = await userManager.FindByEmailAsync(invitation.Email) is not null;
        return TypedResults.Ok(new InvitationDetails(organisation.Name, invitation.Email, invitation.Role, accountExists));
    }

    // Then switch to the organisation (POST /api/organisations/switch) to work in it.
    [Authorize]
    [WolverinePost("/api/join")]
    public static async Task<Results<Ok<JoinOrganisationResponse>, NotFound, ValidationProblem>> Accept(
        JoinOrganisationRequest request,
        ClaimsPrincipal principal,
        IOrganisationStore store,
        OrganisationAccess organisations,
        UserManager<ApplicationUser> userManager,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var invitation = await FindAsync(store, request.Token, clock, cancellationToken);
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        if (invitation is null || user is null)
        {
            return TypedResults.NotFound();
        }

        if (!user.EmailConfirmed || !string.Equals(user.NormalizedEmail, invitation.NormalizedEmail, StringComparison.Ordinal))
        {
            return IdentityResultExtensions.ValidationProblem("Token", $"This invitation is for {invitation.Email}: log in with that email to accept it.");
        }

        await organisations.JoinAsync(invitation, user.Id, cancellationToken);
        return TypedResults.Ok(new JoinOrganisationResponse(invitation.OrganisationId));
    }

    // Creates the account, joins, and signs in: the session starts in the organisation.
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [WolverinePost("/api/join/register")]
    public static async Task<Results<Ok<LoginResponse>, NotFound, ValidationProblem>> Register(
        JoinWithNewAccountRequest request,
        IOrganisationStore store,
        OrganisationAccess organisations,
        UserManager<ApplicationUser> userManager,
        TokenIssuer tokenIssuer,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var invitation = await FindAsync(store, request.Token, clock, cancellationToken);
        if (invitation is null)
        {
            return TypedResults.NotFound();
        }

        if (await userManager.FindByEmailAsync(invitation.Email) is not null)
        {
            return IdentityResultExtensions.ValidationProblem("Token", "You already have an account: log in to accept the invitation.");
        }

        var user = new ApplicationUser { UserName = invitation.Email, Email = invitation.Email, EmailConfirmed = true };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return created.ToValidationProblem();
        }

        await organisations.JoinAsync(invitation, user.Id, cancellationToken);
        return TypedResults.Ok(await tokenIssuer.IssueAsync(user, replacing: null, invitation.OrganisationId, cancellationToken));
    }

    // A valid, unexpired invitation for the link's token.
    private static async Task<OrganisationInvitation?> FindAsync(IOrganisationStore store, string token, TimeProvider clock, CancellationToken cancellationToken)
    {
        var invitation = string.IsNullOrEmpty(token) ? null : await store.FindInvitationByTokenHashAsync(JwtTokenService.HashToken(token), cancellationToken);
        return invitation?.ExpiresAt > clock.GetUtcNow() ? invitation : null;
    }
}
