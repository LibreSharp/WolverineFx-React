using System.Security.Claims;
using ApiHost.Billing;
using ApiHost.Billing.Payments;
using ApiHost.Identity;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wolverine.Http;

namespace ApiHost.Features.Billing;

// Currency: one of the plan's prices' (Billing/Plans.cs); the default currency if left out.
public record CheckoutRequestBody(string PlanId, string? Currency);

public class CheckoutRequestBodyValidator : AbstractValidator<CheckoutRequestBody>
{
    public CheckoutRequestBodyValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Currency).Length(3).When(x => x.Currency is not null);
    }
}

// A page on the payment provider's site to send the browser to.
public record PaymentPageResponse(string Url);

// Subscribing and managing a subscription happen on the payment provider's pages (Stripe Checkout
// and the customer portal), so card details never reach the app. Only owners manage billing. The
// result arrives later by webhook (POST /api/billing/webhooks/{provider}).
public static class PaymentEndpoints
{
    [Authorize]
    [WolverinePost("/api/billing/checkout")]
    public static async Task<Results<Ok<PaymentPageResponse>, ForbidHttpResult, ValidationProblem, ProblemHttpResult>> Checkout(
        CheckoutRequestBody request,
        ClaimsPrincipal principal,
        IOrganisationStore organisations,
        IBillingStore store,
        Entitlements entitlements,
        IPaymentProvider provider,
        UserManager<ApplicationUser> userManager,
        IOptions<BillingOptions> options,
        IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var organisationId = principal.GetOrganisationId();
        if (!await IsOwnerAsync(organisations, organisationId, principal, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        if (!provider.IsConfigured)
        {
            return NotConfigured();
        }

        var plan = PlanCatalogue.Offered.FirstOrDefault(p => p.Id == request.PlanId);
        var currency = (request.Currency ?? options.Value.Currency).ToUpperInvariant();
        if (plan is null || plan.IsFree)
        {
            return IdentityResultExtensions.ValidationProblem("PlanId", "Choose one of the paid plans.");
        }

        if (plan.PriceIn(currency) is null)
        {
            return IdentityResultExtensions.ValidationProblem("Currency", $"The {plan.Name} plan isn't sold in {currency}.");
        }

        var state = await entitlements.StateAsync(organisationId, cancellationToken);
        if (state.Status is BillingStatuses.Active or BillingStatuses.PastDue)
        {
            return IdentityResultExtensions.ValidationProblem("PlanId", "This organisation already has a subscription: change it with Manage billing.");
        }

        var account = await store.FindAccountAsync(organisationId, cancellationToken);
        var user = await userManager.FindByIdAsync(principal.GetUserId());
        var clientUrl = ClientUrl.For(configuration, httpContext);
        var url = await provider.CreateCheckoutAsync(new CheckoutRequest(
            organisationId,
            plan,
            currency,
            account?.CustomerId,
            user?.Email,
            state.TrialEndsAt,
            new Uri($"{clientUrl}/organisation?billing=success"),
            new Uri($"{clientUrl}/organisation")), cancellationToken);
        return TypedResults.Ok(new PaymentPageResponse(url.ToString()));
    }

    // Changing plan, payment details or cancelling, and seeing invoices.
    [Authorize]
    [WolverinePost("/api/billing/portal")]
    public static async Task<Results<Ok<PaymentPageResponse>, ForbidHttpResult, ValidationProblem, ProblemHttpResult>> Portal(
        ClaimsPrincipal principal,
        IOrganisationStore organisations,
        IBillingStore store,
        IPaymentProvider provider,
        IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var organisationId = principal.GetOrganisationId();
        if (!await IsOwnerAsync(organisations, organisationId, principal, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        if (!provider.IsConfigured)
        {
            return NotConfigured();
        }

        var account = await store.FindAccountAsync(organisationId, cancellationToken);
        if (account?.CustomerId is null)
        {
            return IdentityResultExtensions.ValidationProblem("Organisation", "This organisation hasn't subscribed yet.");
        }

        var url = await provider.CreatePortalAsync(account.CustomerId, new Uri($"{ClientUrl.For(configuration, httpContext)}/organisation"), cancellationToken);
        return TypedResults.Ok(new PaymentPageResponse(url.ToString()));
    }

    // The payment provider's webhook: checks the signature, stores the event and answers at once;
    // PaymentEventHandler does the work from a durable queue. Anyone can call it, so a request
    // without a valid signature is refused.
    [AllowAnonymous]
    [WolverinePost("/api/billing/webhooks/{provider}")]
    public static async Task<IResult> Webhook(string provider, HttpRequest request, IPaymentProvider payments, IBillingStore store, CancellationToken cancellationToken)
    {
        if (!string.Equals(provider, payments.Name, StringComparison.Ordinal))
        {
            return TypedResults.NotFound();
        }

        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        if (payments.ReadWebhook(payload, request.Headers) is not { } paymentEvent)
        {
            return TypedResults.BadRequest();
        }

        // A second delivery of the same event is accepted, and ignored.
        await store.RecordPaymentEventAsync(paymentEvent, cancellationToken);
        return TypedResults.Ok();
    }

    private static async Task<bool> IsOwnerAsync(IOrganisationStore organisations, Guid organisationId, ClaimsPrincipal principal, CancellationToken cancellationToken)
        => (await organisations.FindMembershipAsync(organisationId, principal.GetUserId(), cancellationToken))?.Role == OrganisationRoles.Owner;

    private static ProblemHttpResult NotConfigured()
        => TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Payments aren't set up", detail: "Paid plans aren't available yet.");
}
