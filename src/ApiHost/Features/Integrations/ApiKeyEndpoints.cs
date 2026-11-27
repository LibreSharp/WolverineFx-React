using System.Security.Claims;
using ApiHost.Identity;
using ApiHost.Integrations.ApiKeys;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Wolverine.Http;

namespace ApiHost.Features.Integrations;

// An API key as the organisation page lists it: never the key itself.
public record ApiKeySummary(Guid Id, string Name, string Hint, string Scope, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? LastUsedAt, bool Expired)
{
    public static ApiKeySummary Of(ApiKey key, DateTimeOffset now)
        => new(key.Id, key.Name, key.Hint, key.Scope, key.CreatedAt, key.ExpiresAt, key.LastUsedAt, key.IsExpired(now));
}

// Returned once, when a key is created or rotated: Key is the only time the key is shown.
public record CreatedApiKey(ApiKeySummary ApiKey, string Key);

public record CreateApiKeyRequest(string Name, string Scope, int? ExpiresInDays);

public class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public const int MaxNameLength = 100;

    public CreateApiKeyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(x => x.Scope).Must(scope => ApiKeyScopes.All.Contains(scope, StringComparer.Ordinal))
            .WithMessage($"Unknown scope. Scopes: {string.Join(", ", ApiKeyScopes.All)}.");
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, 365);
    }
}

// The current organisation's API keys. Owners and admins manage them; a key can't manage keys
// (ApiKeyOptions.AllowedPaths).
public static class ApiKeyEndpoints
{
    [Authorize]
    [WolverineGet("/api/organisation/api-keys")]
    public static async Task<Results<Ok<IReadOnlyList<ApiKeySummary>>, ForbidHttpResult>> List(
        ClaimsPrincipal user, IOrganisationStore organisations, IApiKeyStore keys, TimeProvider clock, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await CanManageAsync(organisations, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        var now = clock.GetUtcNow();
        IReadOnlyList<ApiKeySummary> list = [.. (await keys.ListAsync(organisationId, cancellationToken)).Select(k => ApiKeySummary.Of(k, now))];
        return TypedResults.Ok(list);
    }

    [Authorize]
    [WolverinePost("/api/organisation/api-keys")]
    public static async Task<Results<Ok<CreatedApiKey>, ForbidHttpResult, ValidationProblem>> Create(
        CreateApiKeyRequest request, ClaimsPrincipal user, IOrganisationStore organisations, IApiKeyStore keys,
        IOptions<ApiKeyOptions> options, TimeProvider clock, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await CanManageAsync(organisations, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        var now = clock.GetUtcNow();
        if ((await keys.ListAsync(organisationId, cancellationToken)).Count(k => !k.IsExpired(now)) >= options.Value.MaxPerOrganisation)
        {
            return IdentityResultExtensions.ValidationProblem("Name", $"An organisation can have up to {options.Value.MaxPerOrganisation} API keys: revoke one first.");
        }

        var expiresAt = request.ExpiresInDays is { } days ? now.AddDays(days) : (DateTimeOffset?)null;
        return TypedResults.Ok(await IssueAsync(keys, organisationId, request.Name.Trim(), request.Scope, expiresAt, user.GetUserId(), now, cancellationToken));
    }

    // A new key with the same name and scope. The old one keeps working for
    // ApiKeyOptions.RotationGracePeriod, so programs can switch over, then expires.
    [Authorize]
    [WolverinePost("/api/organisation/api-keys/{id}/rotate")]
    public static async Task<Results<Ok<CreatedApiKey>, NotFound, ForbidHttpResult>> Rotate(
        Guid id, ClaimsPrincipal user, IOrganisationStore organisations, IApiKeyStore keys,
        IOptions<ApiKeyOptions> options, TimeProvider clock, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await CanManageAsync(organisations, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        var old = await keys.FindAsync(organisationId, id, cancellationToken);
        var now = clock.GetUtcNow();
        if (old is null || old.IsExpired(now))
        {
            return TypedResults.NotFound();
        }

        var graceEnds = now + options.Value.RotationGracePeriod;
        var created = await IssueAsync(keys, organisationId, old.Name, old.Scope, old.ExpiresAt is { } expires && expires > graceEnds ? expires : null, user.GetUserId(), now, cancellationToken);
        old.ExpiresAt = old.ExpiresAt is { } oldExpires && oldExpires < graceEnds ? oldExpires : graceEnds;
        await keys.SaveAsync(old, cancellationToken);
        return TypedResults.Ok(created);
    }

    // Revoking deletes the key: requests with it fail at once.
    [Authorize]
    [WolverineDelete("/api/organisation/api-keys/{id}")]
    public static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Revoke(
        Guid id, ClaimsPrincipal user, IOrganisationStore organisations, IApiKeyStore keys, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await CanManageAsync(organisations, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        var key = await keys.FindAsync(organisationId, id, cancellationToken);
        if (key is null)
        {
            return TypedResults.NotFound();
        }

        await keys.DeleteAsync(key, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<CreatedApiKey> IssueAsync(
        IApiKeyStore keys, Guid organisationId, string name, string scope, DateTimeOffset? expiresAt, string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var secret = ApiKeySecrets.Generate();
        var key = new ApiKey
        {
            Id = Guid.CreateVersion7(),
            OrganisationId = organisationId,
            Name = name,
            Hint = ApiKeySecrets.HintOf(secret),
            SecretHash = ApiKeySecrets.Hash(secret),
            Scope = scope,
            CreatedByUserId = userId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
        await keys.SaveAsync(key, cancellationToken);
        return new CreatedApiKey(ApiKeySummary.Of(key, now), secret);
    }

    private static async Task<bool> CanManageAsync(IOrganisationStore organisations, Guid organisationId, ClaimsPrincipal user, CancellationToken cancellationToken)
        => await organisations.FindMembershipAsync(organisationId, user.GetUserId(), cancellationToken) is { } membership
            && OrganisationRoles.CanManageMembers(membership.Role);
}
