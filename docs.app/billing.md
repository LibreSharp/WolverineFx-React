# Plans, trials and limits

Each organisation is on a plan. A plan includes **features**, which are on or off, and
**limits**, which are numbers. The app checks both in one place, and the client shows or hides
features and offers an upgrade. The code is in `src/ApiHost/Billing/`, the endpoints are in
`Features/Billing/`, and the pages are in `src/Client/src/features/billing/`.

## The plans

The plans are defined in one place: `PlanCatalogue` in `Billing/Plans.cs`. That file sets each
plan's prices per currency (in cents), its features and its limits. The example has two plans:

| | Free | Pro |
|---|---|---|
| Price | Free | $12 or €11 a month |
| Open Todos | 25 | Unlimited |
| Members (invitations count) | 3 | 50 |
| Todo reminders | No | Yes, 1,000 a month |

- **Versions keep existing customers' prices.** To change a plan, add a new version to
  `PlanCatalogue.All` instead of editing the existing one. An organisation keeps the version it
  subscribed to, with its price and limits, until it changes plan ("grandfathering"). New
  subscribers get the latest version.
- **A free plan is a plan with a price of zero.** An organisation that isn't paying is on it.
- **The pricing page** (`/pricing`) is pre-rendered from a copy of the catalogue,
  `src/Client/src/features/billing/plans.json`. A test fails when the copy no longer matches
  `GET /api/billing/plans`. To update it after changing the plans, run:
  `UPDATE_PLANS=1 dotnet test --filter the_plans_on_offer`.

## Trials

New organisations get a **reverse trial**: the top plan (`Billing__TrialPlan`, `pro`) for 14 days
(`Billing__TrialDays`; 0 turns trials off), then the free plan unless they subscribe. Nothing
needs to run when a trial ends. The plan is worked out from the time and the organisation's
billing account each time it's needed (`BillingState.Resolve`).

## Checking features and limits

`Entitlements` (`Billing/Entitlements.cs`) answers "can this organisation use feature X?" and
"how much of limit Y is left?". Use it in endpoints and handlers, and never check a plan's name.

- **Features** are feature flags, using [Microsoft.FeatureManagement](https://github.com/microsoft/FeatureManagement-Dotnet).
  Each feature in `PlanFeatures` is a flag that is on for organisations whose plan includes it
  (`PlanFeatureFlags.cs`). Other flags come from the `FeatureManagement` configuration section as
  usual. Check either kind with `entitlements.IsEnabledAsync(organisationId, feature)`.
- **Count limits** count what the organisation has now, such as open Todos or members. The
  feature that owns the data counts it: an `ILimitUsage`, registered in `BillingSetup`. Check
  with `CanAddAsync`. Two requests at the same moment can both take the last place, so a Count
  limit can be passed by one or two.
- **PerPeriod limits** count use in the current billing period, such as reminders a month.
  `TryUseAsync` adds to the count only while it stays within the limit, in one SQL statement, so
  requests at the same moment can't pass it. The count starts again each period: the paid
  subscription's period, or otherwise a month at a time from the day the organisation was
  created.

An endpoint checks before it runs, in a Wolverine `Before` method. If that method returns a
problem, Wolverine stops there:

```csharp
public static Task<ProblemDetails> BeforeAsync(ClaimsPrincipal user, Entitlements entitlements, CancellationToken cancellationToken)
    => entitlements.RequireFeatureAsync(user.GetOrganisationId(), PlanFeatures.TodoReminders, cancellationToken);
```

The answer is **402 Payment Required**: ProblemDetails with a `detail` for the user, and a
`feature` or `limit` field naming what's missing. The client shows the detail. To show or hide
features in advance, the client reads `GET /api/billing` (`useBilling()`), which returns the
plan, its features and how much of each limit is used. It warns at 80% and stops at 100%.

## Downgrades never delete data

When an organisation moves to a smaller plan, anything over a limit stays. It can't add more until
it's back under the limit: a 26th open Todo on Free is refused, but completing Todos makes room.
Paid features switch off at once, including reminders already scheduled, which are dropped when
they come due.

## Adding a feature or a limit

1. Add a constant to `PlanFeatures` (with a description) or `PlanLimits.All` in `Billing/Plans.cs`,
   and give it to the plans that include it. Add a new plan version if customers are already on
   the plan.
2. For a Count limit, add an `ILimitUsage` that counts it, and register it in `BillingSetup`.
3. Check it in the endpoint (`RequireFeatureAsync`, `RequireRoomAsync`, or `TryUseAsync`), and
   in any handler that runs later.
4. Update the pricing page's copy (above), and show or hide the feature in the client with
   `useBilling()`.
