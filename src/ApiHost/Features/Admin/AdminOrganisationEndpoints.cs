using System.Security.Claims;
using ApiHost.Billing;
using ApiHost.Billing.Payments;
using ApiHost.Identity;
using ApiHost.Organisations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Wolverine.Http;

namespace ApiHost.Features.Admin;

public record AdminOrganisationRow(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    int Members,
    string PlanName,
    string Status,
    DateTimeOffset? TrialEndsAt,
    bool Suspended);

public record AdminOrganisationList(IReadOnlyList<AdminOrganisationRow> Organisations, int Total, int Page, int PageSize);

// Links: to the payment provider's dashboard, when there's a provider.
public record AdminInvoice(string Id, string? Number, string Status, long Total, long AmountPaid, string Currency, DateTimeOffset CreatedAt, string? HostedUrl, string? DashboardUrl);

// Enabled: what the organisation gets now; Override: the admin's override for this organisation,
// if any.
public record AdminOrganisationFeature(string Name, string Description, bool Enabled, bool? Override);

public record AdminOrganisationBilling(
    string PlanId,
    string PlanName,
    int PlanVersion,
    string Status,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    bool CancelAtPeriodEnd,
    string? Currency,
    string? CustomerUrl,
    string? SubscriptionUrl);

public record AdminOrganisationDetail(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SuspendedAt,
    string? SuspensionReason,
    string? Notes,
    IReadOnlyList<OrganisationMember> Members,
    AdminOrganisationBilling Billing,
    IReadOnlyList<LimitUsage> Limits,
    IReadOnlyList<AdminInvoice> Invoices,
    IReadOnlyList<AdminOrganisationFeature> Features);

// Until: the plan applies until then, as a trial does; then the free plan, unless they subscribe.
public record GrantPlanRequest(string PlanId, DateTimeOffset Until);

public class GrantPlanRequestValidator : AbstractValidator<GrantPlanRequest>
{
    public GrantPlanRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.PlanId).Must(id => PlanCatalogue.Offered.Any(p => p.Id == id)).WithMessage("Unknown plan.");
        RuleFor(x => x.Until).Must(until => until > clock.GetUtcNow()).WithMessage("Choose a date in the future.");
    }
}

public record SuspendOrganisationRequest(string Reason);

public class SuspendOrganisationRequestValidator : AbstractValidator<SuspendOrganisationRequest>
{
    public SuspendOrganisationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public record OrganisationNotesRequest(string? Notes);

public class OrganisationNotesRequestValidator : AbstractValidator<OrganisationNotesRequest>
{
    public OrganisationNotesRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(10_000);
    }
}

// The back office's organisations: find one, see its members, plan, usage and invoices, and
// support it: give it a plan for a while (or extend its trial), suspend it, keep notes, or
// delete it. Every change goes in the audit log.
public static class AdminOrganisationEndpoints
{
    public const int PageSize = 25;

    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/organisations")]
    public static async Task<AdminOrganisationList> List(
        string? search, int? page, IBackOfficeQueries queries, IOptions<BillingOptions> options, TimeProvider clock, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(page ?? 1, 1);
        var (rows, total) = await queries.OrganisationsAsync(search, pageNumber, PageSize, cancellationToken);
        var accounts = (await queries.AccountsAsync([.. rows.Select(r => r.Id)], cancellationToken)).ToDictionary(a => a.Id);
        var now = clock.GetUtcNow();
        return new AdminOrganisationList(
            [.. rows.Select(row =>
            {
                var state = BillingState.Resolve(
                    new Organisation { Id = row.Id, CreatedAt = row.CreatedAt }, accounts.GetValueOrDefault(row.Id), options.Value, now);
                return new AdminOrganisationRow(row.Id, row.Name, row.CreatedAt, row.Members, state.Plan.Name, state.Status, state.TrialEndsAt, row.SuspendedAt is not null);
            })],
            total,
            pageNumber,
            PageSize);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/organisations/{id}")]
    public static async Task<Results<Ok<AdminOrganisationDetail>, NotFound>> Get(
        Guid id,
        IOrganisationStore organisations,
        IBillingStore billing,
        Entitlements entitlements,
        IPaymentProvider provider,
        CancellationToken cancellationToken)
    {
        var organisation = await organisations.FindAsync(id, cancellationToken);
        if (organisation is null)
        {
            return TypedResults.NotFound();
        }

        var state = await entitlements.StateAsync(id, cancellationToken);
        var account = await billing.FindAccountAsync(id, cancellationToken);
        var overrides = await billing.FeatureOverridesAsync(cancellationToken);
        var features = new List<AdminOrganisationFeature>();
        foreach (var (name, description) in PlanFeatures.Descriptions)
        {
            var own = overrides.FirstOrDefault(o => o.Id == FeatureOverride.IdFor(FeatureScopes.Organisation, id.ToString(), name));
            features.Add(new AdminOrganisationFeature(name, description, await entitlements.IsEnabledAsync(id, name, cancellationToken), own?.Enabled));
        }

        string? Link(string kind, string? providerId)
            => providerId is null ? null : provider.DashboardLink(kind, providerId)?.ToString();

        return TypedResults.Ok(new AdminOrganisationDetail(
            organisation.Id,
            organisation.Name,
            organisation.CreatedAt,
            organisation.SuspendedAt,
            organisation.SuspensionReason,
            organisation.AdminNotes,
            await organisations.MembersAsync(id, cancellationToken),
            new AdminOrganisationBilling(
                state.Plan.Id, state.Plan.Name, state.Plan.Version, state.Status, state.TrialEndsAt, state.PeriodStart, state.PeriodEnd,
                state.CancelAtPeriodEnd, account?.Currency, Link("customer", account?.CustomerId), Link("subscription", account?.SubscriptionId)),
            await entitlements.AllUsageAsync(id, cancellationToken),
            [.. (await billing.InvoicesAsync(id, cancellationToken)).Select(i => new AdminInvoice(
                i.Id, i.Number, i.Status, i.Total, i.AmountPaid, i.Currency, i.CreatedAt, i.HostedUrl, Link("invoice", i.Id)))],
            features));
    }

    // Puts the organisation on a plan until a date, free of charge: to extend a trial, or to give
    // a customer a plan. Not for an organisation that's subscribed: change that in the provider.
    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePost("/api/admin/organisations/{id}/grant")]
    public static async Task<Results<NoContent, NotFound, ValidationProblem>> Grant(
        Guid id,
        GrantPlanRequest request,
        ClaimsPrincipal principal,
        IOrganisationStore organisations,
        IBillingStore billing,
        IAuditLog auditLog,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (await organisations.FindAsync(id, cancellationToken) is null)
        {
            return TypedResults.NotFound();
        }

        var account = await billing.FindAccountAsync(id, cancellationToken) ?? new BillingAccount { Id = id };
        if (account.Status is SubscriptionStatuses.Active or SubscriptionStatuses.PastDue)
        {
            return IdentityResultExtensions.ValidationProblem("PlanId", "This organisation has a subscription: change its plan with the payment provider.");
        }

        var plan = PlanCatalogue.Latest(request.PlanId);
        account.PlanId = plan.Id;
        account.PlanVersion = plan.Version;
        account.Status = SubscriptionStatuses.Trialing;
        account.TrialEndsAt = request.Until.ToUniversalTime();
        account.UpdatedAt = clock.GetUtcNow();
        await billing.SaveAccountAsync(account, cancellationToken);
        await auditLog.RecordAsync(principal, AuditActions.GrantPlan, "Organisation", id.ToString(), $"{plan.Name} until {request.Until:yyyy-MM-dd}", cancellationToken);
        return TypedResults.NoContent();
    }

    // Nobody can work in a suspended organisation (OrganisationSetup checks every request); its
    // members' sessions move to their other organisations. Nothing is deleted.
    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePost("/api/admin/organisations/{id}/suspend")]
    public static async Task<Results<NoContent, NotFound>> Suspend(
        Guid id, SuspendOrganisationRequest request, ClaimsPrincipal principal, IOrganisationStore organisations, IAuditLog auditLog, TimeProvider clock, CancellationToken cancellationToken)
    {
        var organisation = await organisations.FindAsync(id, cancellationToken);
        if (organisation is null)
        {
            return TypedResults.NotFound();
        }

        organisation.SuspendedAt = clock.GetUtcNow();
        organisation.SuspensionReason = request.Reason.Trim();
        await organisations.SaveAsync(organisation, cancellationToken);
        await auditLog.RecordAsync(principal, AuditActions.Suspend, "Organisation", id.ToString(), organisation.SuspensionReason, cancellationToken);
        return TypedResults.NoContent();
    }

    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePost("/api/admin/organisations/{id}/unsuspend")]
    public static async Task<Results<NoContent, NotFound>> Unsuspend(
        Guid id, ClaimsPrincipal principal, IOrganisationStore organisations, IAuditLog auditLog, CancellationToken cancellationToken)
    {
        var organisation = await organisations.FindAsync(id, cancellationToken);
        if (organisation is null)
        {
            return TypedResults.NotFound();
        }

        organisation.SuspendedAt = null;
        organisation.SuspensionReason = null;
        await organisations.SaveAsync(organisation, cancellationToken);
        await auditLog.RecordAsync(principal, AuditActions.Unsuspend, "Organisation", id.ToString(), null, cancellationToken);
        return TypedResults.NoContent();
    }

    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePut("/api/admin/organisations/{id}/notes")]
    public static async Task<Results<NoContent, NotFound>> Notes(
        Guid id, OrganisationNotesRequest request, ClaimsPrincipal principal, IOrganisationStore organisations, IAuditLog auditLog, CancellationToken cancellationToken)
    {
        var organisation = await organisations.FindAsync(id, cancellationToken);
        if (organisation is null)
        {
            return TypedResults.NotFound();
        }

        organisation.AdminNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await organisations.SaveAsync(organisation, cancellationToken);
        await auditLog.RecordAsync(principal, AuditActions.EditNotes, "Organisation", id.ToString(), null, cancellationToken);
        return TypedResults.NoContent();
    }

    // Deletes it for everyone, with its data, as its owner can; its subscription is cancelled.
    [Authorize(Roles = AppRoles.Admin)]
    [WolverineDelete("/api/admin/organisations/{id}")]
    public static async Task<Results<NoContent, NotFound>> Delete(
        Guid id, ClaimsPrincipal principal, IOrganisationStore organisations, IAuditLog auditLog, CancellationToken cancellationToken)
    {
        var organisation = await organisations.FindAsync(id, cancellationToken);
        if (organisation is null)
        {
            return TypedResults.NotFound();
        }

        await organisations.DeleteAsync(id, cancellationToken);
        await auditLog.RecordAsync(principal, AuditActions.DeleteOrganisation, "Organisation", id.ToString(), organisation.Name, cancellationToken);
        return TypedResults.NoContent();
    }

    // A user's organisations, for the back office's users list.
    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/users/{id}/organisations")]
    public static Task<IReadOnlyList<UserOrganisation>> OfUser(string id, IOrganisationStore organisations, CancellationToken cancellationToken)
        => organisations.OrganisationsOfUserAsync(id, null, cancellationToken);
}
