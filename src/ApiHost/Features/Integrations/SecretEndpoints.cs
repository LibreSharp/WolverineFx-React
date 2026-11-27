using System.Security.Claims;
using System.Text.RegularExpressions;
using ApiHost.Identity;
using ApiHost.Integrations.Secrets;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace ApiHost.Features.Integrations;

public record SetSecretRequest(string Value);

public class SetSecretRequestValidator : AbstractValidator<SetSecretRequest>
{
    public SetSecretRequestValidator()
    {
        RuleFor(x => x.Value).NotEmpty().MaximumLength(10_000);
    }
}

// The current organisation's secrets. Owners set and delete them; nobody can read a value back
// through the API: only the app's own code can (OrganisationSecrets.GetAsync).
public static partial class SecretEndpoints
{
    [Authorize]
    [WolverineGet("/api/organisation/secrets")]
    public static async Task<Results<Ok<IReadOnlyList<SecretSummary>>, ForbidHttpResult>> List(
        ClaimsPrincipal user, IOrganisationStore organisations, OrganisationSecrets secrets, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        return await IsOwnerAsync(organisations, organisationId, user, cancellationToken)
            ? TypedResults.Ok(await secrets.ListAsync(organisationId, cancellationToken))
            : TypedResults.Forbid();
    }

    [Authorize]
    [WolverinePut("/api/organisation/secrets/{name}")]
    public static async Task<Results<NoContent, ForbidHttpResult, ValidationProblem>> Set(
        string name, SetSecretRequest request, ClaimsPrincipal user, IOrganisationStore organisations, OrganisationSecrets secrets, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await IsOwnerAsync(organisations, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        if (!SecretName().IsMatch(name))
        {
            return IdentityResultExtensions.ValidationProblem("Name", "Use lower case letters, digits and dashes (up to 64), such as openai-api-key.");
        }

        await secrets.SetAsync(organisationId, name, request.Value, user.GetUserId(), cancellationToken);
        return TypedResults.NoContent();
    }

    [Authorize]
    [WolverineDelete("/api/organisation/secrets/{name}")]
    public static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Delete(
        string name, ClaimsPrincipal user, IOrganisationStore organisations, OrganisationSecrets secrets, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await IsOwnerAsync(organisations, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        return await secrets.DeleteAsync(organisationId, name, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<bool> IsOwnerAsync(IOrganisationStore organisations, Guid organisationId, ClaimsPrincipal user, CancellationToken cancellationToken)
        => (await organisations.FindMembershipAsync(organisationId, user.GetUserId(), cancellationToken))?.Role == OrganisationRoles.Owner;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SecretName();
}
