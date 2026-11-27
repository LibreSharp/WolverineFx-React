using ApiHost.Identity;
using System.Security.Claims;
using ApiHost.Identity.Email;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Admin;

public record SetRolesRequest(IReadOnlyList<string> Roles);

public class SetRolesRequestValidator : AbstractValidator<SetRolesRequest>
{
    public SetRolesRequestValidator()
    {
        RuleForEach(x => x.Roles).Must(role => AppRoles.All.Contains(role, StringComparer.Ordinal))
            .WithMessage(role => $"Unknown role. Roles: {string.Join(", ", AppRoles.All)}.");
    }
}

public record InviteUserRequest(string Email, IReadOnlyList<string>? Roles);

public class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleForEach(x => x.Roles).Must(role => AppRoles.All.Contains(role, StringComparer.Ordinal))
            .WithMessage(role => $"Unknown role. Roles: {string.Join(", ", AppRoles.All)}.");
    }
}

// Admin actions on accounts, through ASP.NET Core Identity's UserManager. An admin can't lock
// their own account or remove their own Admin role, so there's always an admin left to undo it.
public static class ManageUserEndpoints
{
    // Locks the account until an admin unlocks it, and ends its sessions: refresh tokens are
    // revoked now, and access tokens expire within their lifetime (Jwt:AccessTokenLifetimeMinutes).
    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePost("/api/admin/users/{id}/lock")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> Lock(
        string id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenStore refreshTokenStore,
        CancellationToken cancellationToken)
    {
        if (id == principal.GetUserId())
        {
            return IdentityResultExtensions.ValidationProblem("Id", "You can't lock your own account.");
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        // Lockout only applies to accounts with it enabled (Identity's LockoutEnabled flag).
        await userManager.SetLockoutEnabledAsync(user, true);
        var result = await userManager.SetLockoutEndDateAsync(user, Lockout.Indefinitely);
        if (!result.Succeeded)
        {
            return result.ToValidationProblem();
        }

        await refreshTokenStore.RevokeAllForUserAsync(user.Id, cancellationToken);
        return TypedResults.NoContent();
    }

    // Lifts any lockout: an admin's, or one from failed logins.
    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePost("/api/admin/users/{id}/unlock")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> Unlock(
        string id, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var result = await userManager.SetLockoutEndDateAsync(user, null);
        if (result.Succeeded)
        {
            result = await userManager.ResetAccessFailedCountAsync(user);
        }

        return result.Succeeded ? TypedResults.NoContent() : result.ToValidationProblem();
    }

    // Replaces the user's roles. The new roles are in their access token from their next login or
    // token refresh (within Jwt:AccessTokenLifetimeMinutes).
    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePut("/api/admin/users/{id}/roles")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> SetRoles(
        string id, SetRolesRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (id == principal.GetUserId() && !request.Roles.Contains(AppRoles.Admin, StringComparer.Ordinal))
        {
            return IdentityResultExtensions.ValidationProblem("Roles", "You can't remove your own Admin role.");
        }

        var current = await userManager.GetRolesAsync(user);
        var result = await userManager.RemoveFromRolesAsync(user, current.Except(request.Roles, StringComparer.Ordinal));
        if (result.Succeeded)
        {
            result = await userManager.AddToRolesAsync(user, request.Roles.Distinct(StringComparer.Ordinal).Except(current, StringComparer.Ordinal));
        }

        return result.Succeeded ? TypedResults.NoContent() : result.ToValidationProblem();
    }

    // Creates an account with no password and emails the person a link to choose one (see
    // AcceptInvitationEndpoint). Inviting someone whose invitation is still pending sends it again.
    // Works whether or not open registration is on (Accounts:AllowRegistration).
    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePost("/api/admin/invitations")]
    public static async Task<(Results<Accepted, ValidationProblem>, OutgoingMessages)> Invite(
        InviteUserRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        HttpContext httpContext)
    {
        var admin = await userManager.FindByIdAsync(principal.GetUserId());
        var invitedBy = admin?.DisplayName ?? admin?.Email ?? "An administrator";

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null && (user.EmailConfirmed || await userManager.HasPasswordAsync(user)))
        {
            return (IdentityResultExtensions.ValidationProblem("Email", "Someone with this email already has an account."), []);
        }

        if (user is null)
        {
            user = new ApplicationUser { UserName = request.Email, Email = request.Email };
            var result = await userManager.CreateAsync(user);
            if (result.Succeeded && request.Roles is { Count: > 0 })
            {
                result = await userManager.AddToRolesAsync(user, request.Roles.Distinct(StringComparer.Ordinal));
            }

            if (!result.Succeeded)
            {
                return (result.ToValidationProblem(), []);
            }
        }

        return (TypedResults.Accepted((string?)null), [new SendInvitation(user.Id, invitedBy, ClientUrl.For(configuration, httpContext))]);
    }
}
