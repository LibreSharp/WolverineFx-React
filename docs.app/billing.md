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

## Payments with Stripe

Paid plans are sold through [Stripe](https://stripe.com/) (Stripe charges fees). Until its keys
are set, the app offers no paid plans: just the trial and the free plan.

- **Subscribing** happens on Stripe's own pages, so card details never reach the app. An owner
  presses Upgrade on `/organisation`, `POST /api/billing/checkout` opens Stripe Checkout, and
  Stripe brings them back. Whatever is left of the trial carries on, and Stripe charges when it
  ends. Changing plan or payment details, invoices and cancelling are in Stripe's customer
  portal (Manage billing; `POST /api/billing/portal`).
- **Webhooks keep the app's copy in step.** Stripe calls `POST /api/billing/webhooks/stripe` for
  each change. The endpoint checks the signature, stores the event and answers at once. A second
  delivery of the same event is ignored. A Wolverine handler (`Billing/Payments/PaymentEventHandler.cs`)
  then reads the subscription from Stripe and updates the organisation's `BillingAccount`: plan
  and version, status, billing period and invoices. It runs from a durable queue, so a failure
  is retried, and then parked with Wolverine's failed messages. Answering "which plan is this
  organisation on?" never calls Stripe.
- **A failed payment** (`PastDue`) keeps the plan while Stripe retries. When Stripe gives up, or
  the customer cancels, the subscription ends and the organisation is on the free plan.
- **Deleting an organisation** cancels its subscription.
- **Another provider** (such as Paddle or Lemon Squeezy, which handle sales tax and VAT as the
  seller) implements `IPaymentProvider` (`Billing/Payments/`) and is registered in `BillingSetup`
  instead. Nothing else changes.

### Setting it up

1. In the [Stripe dashboard](https://dashboard.stripe.com/), in test mode, copy the secret key
   (`sk_test_...`) into user secrets:
   `dotnet user-secrets --project src/ApiHost set Billing:Stripe:SecretKey sk_test_...`
2. Create the products and prices from `Billing/Plans.cs`:
   `dotnet run --project src/ApiHost -- stripe-prices`. Each plan version and currency becomes a
   price with a lookup key (such as `pro-v1-usd`), which checkout finds it by. Run it again after
   adding a plan version: it only adds what's missing. Prices can't change in Stripe, so a new
   price means a new plan version.
3. Forward webhooks to the app with the [Stripe CLI](https://docs.stripe.com/stripe-cli):
   `stripe listen --forward-to localhost:5133/api/billing/webhooks/stripe`. Put the signing
   secret it prints (`whsec_...`) in `Billing:Stripe:WebhookSecret`.
4. Turn on the customer portal in the dashboard (Settings > Billing > Customer portal), with the
   plans customers may switch between.
5. Subscribe with a [test card](https://docs.stripe.com/testing), such as 4242 4242 4242 4242.

In production, use the live keys (`Billing__Stripe__SecretKey`, `Billing__Stripe__WebhookSecret`;
see [deployment](deployment.md)), run `stripe-prices` with the live key, and add a webhook
endpoint for `https://your-domain/api/billing/webhooks/stripe` with these events:
`checkout.session.completed`, `customer.subscription.created`, `customer.subscription.updated`,
`customer.subscription.deleted`, `invoice.paid` and `invoice.payment_failed`.
`Billing__Stripe__AutomaticTax=true` has Stripe Tax add sales tax and VAT.

## Adding a feature or a limit

1. Add a constant to `PlanFeatures` (with a description) or `PlanLimits.All` in `Billing/Plans.cs`,
   and give it to the plans that include it. Add a new plan version if customers are already on
   the plan.
2. For a Count limit, add an `ILimitUsage` that counts it, and register it in `BillingSetup`.
3. Check it in the endpoint (`RequireFeatureAsync`, `RequireRoomAsync`, or `TryUseAsync`), and
   in any handler that runs later.
4. Update the pricing page's copy (above), and show or hide the feature in the client with
   `useBilling()`.
