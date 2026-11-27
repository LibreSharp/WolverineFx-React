using System.Security.Claims;
using ApiHost.Identity;
using ApiHost.Integrations.Secrets;
using ApiHost.Integrations.Webhooks;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.Http;

namespace ApiHost.Features.Integrations;

public record WebhookSummary(Guid Id, string Url, string Description, IReadOnlyList<string> Events, bool Enabled, DateTimeOffset CreatedAt)
{
    public static WebhookSummary Of(WebhookSubscription s) => new(s.Id, s.Url, s.Description, [.. s.Events], s.Enabled, s.CreatedAt);
}

// Returned when a webhook is created, and by "show secret": the secret receivers check signatures with.
public record WebhookSecret(string Secret);

public record CreatedWebhook(WebhookSummary Webhook, string Secret);

public record WebhookEventType(string Type, string Description);

public record DeliverySummary(Guid Id, Guid EventId, string EventType, DateTimeOffset CreatedAt, int Attempts, DateTimeOffset? LastAttemptAt, int? StatusCode, string? Response, DateTimeOffset? DeliveredAt, string Payload)
{
    public static DeliverySummary Of(WebhookDelivery d) => new(d.Id, d.EventId, d.EventType, d.CreatedAt, d.Attempts, d.LastAttemptAt, d.StatusCode, d.Response, d.DeliveredAt, d.Payload);
}

public record SaveWebhookRequest(string Url, string? Description, IReadOnlyList<string>? Events, bool Enabled = true);

public class SaveWebhookRequestValidator : AbstractValidator<SaveWebhookRequest>
{
    public SaveWebhookRequestValidator(IOptions<WebhookOptions> options)
    {
        var allowPrivate = options.Value.AllowPrivateNetworks;
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2000)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || (allowPrivate && uri.Scheme == Uri.UriSchemeHttp)))
            .WithMessage(allowPrivate ? "Enter an http:// or https:// URL." : "Enter an https:// URL.");
        RuleFor(x => x.Description).MaximumLength(200);
        RuleForEach(x => x.Events).Must(type => WebhookEvents.Descriptions.ContainsKey(type))
            .WithMessage("Unknown event type.");
    }
}

// The current organisation's webhooks: owners and admins manage them, see each one's deliveries,
// and resend.
public static class WebhookEndpoints
{
    // How many deliveries a webhook's page shows.
    private const int RecentDeliveries = 50;

    [Authorize]
    [WolverineGet("/api/organisation/webhooks/events")]
    public static IReadOnlyList<WebhookEventType> EventTypes()
        => [.. WebhookEvents.Descriptions.Select(e => new WebhookEventType(e.Key, e.Value))];

    [Authorize]
    [WolverineGet("/api/organisation/webhooks")]
    public static async Task<Results<Ok<IReadOnlyList<WebhookSummary>>, ForbidHttpResult>> List(
        ClaimsPrincipal user, IOrganisationStore organisations, IWebhookStore webhooks, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await CanManageAsync(organisations, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        IReadOnlyList<WebhookSummary> list = [.. (await webhooks.SubscriptionsAsync(organisationId, cancellationToken)).Select(WebhookSummary.Of)];
        return TypedResults.Ok(list);
    }

    [Authorize]
    [WolverinePost("/api/organisation/webhooks")]
    public static async Task<Results<Ok<CreatedWebhook>, ForbidHttpResult, ValidationProblem>> Create(
        SaveWebhookRequest request, ClaimsPrincipal user, IOrganisationStore organisations, IWebhookStore webhooks,
        SecretProtection protection, IOptions<WebhookOptions> options, TimeProvider clock, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await CanManageAsync(organisations, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        if ((await webhooks.SubscriptionsAsync(organisationId, cancellationToken)).Count >= options.Value.MaxPerOrganisation)
        {
            return IdentityResultExtensions.ValidationProblem("Url", $"An organisation can have up to {options.Value.MaxPerOrganisation} webhooks.");
        }

        var secret = WebhookSignatures.GenerateSecret();
        var subscription = new WebhookSubscription { Id = Guid.CreateVersion7(), OrganisationId = organisationId, CreatedAt = clock.GetUtcNow() };
        Apply(request, subscription);
        subscription.ProtectedSecret = protection.Protect(WebhookSender.SecretPurpose, subscription.Id.ToString(), secret);
        await webhooks.SaveSubscriptionAsync(subscription, cancellationToken);
        return TypedResults.Ok(new CreatedWebhook(WebhookSummary.Of(subscription), secret));
    }

    [Authorize]
    [WolverinePut("/api/organisation/webhooks/{id}")]
    public static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Update(
        Guid id, SaveWebhookRequest request, ClaimsPrincipal user, IOrganisationStore organisations, IWebhookStore webhooks, CancellationToken cancellationToken)
    {
        var (subscription, forbidden) = await FindAsync(id, user, organisations, webhooks, cancellationToken);
        if (subscription is null)
        {
            return forbidden ? TypedResults.Forbid() : TypedResults.NotFound();
        }

        Apply(request, subscription);
        await webhooks.SaveSubscriptionAsync(subscription, cancellationToken);
        return TypedResults.NoContent();
    }

    [Authorize]
    [WolverineDelete("/api/organisation/webhooks/{id}")]
    public static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Delete(
        Guid id, ClaimsPrincipal user, IOrganisationStore organisations, IWebhookStore webhooks, CancellationToken cancellationToken)
    {
        var (subscription, forbidden) = await FindAsync(id, user, organisations, webhooks, cancellationToken);
        if (subscription is null)
        {
            return forbidden ? TypedResults.Forbid() : TypedResults.NotFound();
        }

        await webhooks.DeleteSubscriptionAsync(subscription, cancellationToken);
        return TypedResults.NoContent();
    }

    [Authorize]
    [WolverineGet("/api/organisation/webhooks/{id}/secret")]
    public static async Task<Results<Ok<WebhookSecret>, NotFound, ForbidHttpResult>> Secret(
        Guid id, ClaimsPrincipal user, IOrganisationStore organisations, IWebhookStore webhooks, SecretProtection protection, CancellationToken cancellationToken)
    {
        var (subscription, forbidden) = await FindAsync(id, user, organisations, webhooks, cancellationToken);
        if (subscription is null)
        {
            return forbidden ? TypedResults.Forbid() : TypedResults.NotFound();
        }

        return TypedResults.Ok(new WebhookSecret(protection.Unprotect(WebhookSender.SecretPurpose, subscription.Id.ToString(), subscription.ProtectedSecret)));
    }

    [Authorize]
    [WolverineGet("/api/organisation/webhooks/{id}/deliveries")]
    public static async Task<Results<Ok<IReadOnlyList<DeliverySummary>>, NotFound, ForbidHttpResult>> Deliveries(
        Guid id, ClaimsPrincipal user, IOrganisationStore organisations, IWebhookStore webhooks, CancellationToken cancellationToken)
    {
        var (subscription, forbidden) = await FindAsync(id, user, organisations, webhooks, cancellationToken);
        if (subscription is null)
        {
            return forbidden ? TypedResults.Forbid() : TypedResults.NotFound();
        }

        IReadOnlyList<DeliverySummary> list = [.. (await webhooks.DeliveriesAsync(subscription.OrganisationId, subscription.Id, RecentDeliveries, cancellationToken)).Select(DeliverySummary.Of)];
        return TypedResults.Ok(list);
    }

    // Sends a "webhook.test" event to this webhook only, to check the receiver.
    [Authorize]
    [WolverinePost("/api/organisation/webhooks/{id}/test")]
    public static async Task<Results<Accepted, NotFound, ForbidHttpResult>> Test(
        Guid id, ClaimsPrincipal user, IOrganisationStore organisations, IWebhookStore webhooks, IMessageBus bus,
        IOptions<WebhookOptions> options, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (subscription, forbidden) = await FindAsync(id, user, organisations, webhooks, cancellationToken);
        if (subscription is null)
        {
            return forbidden ? TypedResults.Forbid() : TypedResults.NotFound();
        }

        var raised = WebhookEvents.Raise(subscription.OrganisationId, WebhookEvents.Test, new { message = "A test event from the organisation's webhook settings." });
        foreach (var message in await WebhookHandler.DeliverAsync(raised, [subscription], webhooks, options.Value, clock, cancellationToken))
        {
            await bus.PublishAsync(message);
        }

        return TypedResults.Accepted((string?)null);
    }

    [Authorize]
    [WolverinePost("/api/organisation/webhooks/deliveries/{deliveryId}/resend")]
    public static async Task<Results<Accepted, NotFound, ForbidHttpResult>> Resend(
        Guid deliveryId, ClaimsPrincipal user, IOrganisationStore organisations, IWebhookStore webhooks, IMessageBus bus, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await CanManageAsync(organisations, organisationId, user, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        if (await webhooks.FindDeliveryAsync(organisationId, deliveryId, cancellationToken) is null)
        {
            return TypedResults.NotFound();
        }

        await bus.PublishAsync(new DeliverWebhook(deliveryId, Again: true));
        return TypedResults.Accepted((string?)null);
    }

    private static void Apply(SaveWebhookRequest request, WebhookSubscription subscription)
    {
        subscription.Url = request.Url.Trim();
        subscription.Description = request.Description?.Trim() ?? string.Empty;
        subscription.Events = [.. (request.Events ?? []).Distinct(StringComparer.Ordinal)];
        subscription.Enabled = request.Enabled;
    }

    // The webhook, or null and whether that's because the user may not manage webhooks.
    private static async Task<(WebhookSubscription? Subscription, bool Forbidden)> FindAsync(
        Guid id, ClaimsPrincipal user, IOrganisationStore organisations, IWebhookStore webhooks, CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        if (!await CanManageAsync(organisations, organisationId, user, cancellationToken))
        {
            return (null, true);
        }

        return (await webhooks.FindSubscriptionAsync(organisationId, id, cancellationToken), false);
    }

    public static async Task<bool> CanManageAsync(IOrganisationStore organisations, Guid organisationId, ClaimsPrincipal user, CancellationToken cancellationToken)
        => await organisations.FindMembershipAsync(organisationId, user.GetUserId(), cancellationToken) is { } membership
            && OrganisationRoles.CanManageMembers(membership.Role);
}
