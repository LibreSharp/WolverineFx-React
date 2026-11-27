# Integrations

Ways for other programs to work with an organisation's data: **API keys**, an **MCP endpoint**
for AI assistants, **outgoing webhooks**, **encrypted secrets**, and **receiving webhooks**. The
code is in `src/ApiHost/Integrations/`, the endpoints in `Features/Integrations/`, and the page
(`/organisation/integrations`, for owners and admins) in `src/Client/src/features/integrations/`.

## API keys

An owner or admin creates a key on the Integrations page. The key (`sk_...`) is shown once; only
its SHA-256 hash is stored. A program sends it as a bearer token:

```bash
curl -H "Authorization: Bearer sk_..." https://your-domain/api/todos
```

- **A key acts as the member who created it, in its organisation.** Tenancy, plan limits and
  realtime updates work as they do for that member's own requests. The key stops working when
  it's revoked or expires, when that member leaves the organisation, or when the organisation is
  suspended or deleted.
- **Scopes:** `read` keys can only make GET requests (and use the MCP tools that only read);
  `write` keys can do everything a key may do.
- **Keys only reach the paths they're for**, `ApiKeys:AllowedPaths` (`/api/todos` and `/mcp`).
  Account, organisation, billing and admin endpoints, and the realtime hub, need a user's login.
  Add your own API's paths there.
- **Rate limits:** each key gets `ApiKeys:RequestsPerMinute` (120) requests a minute, then 429.
- **Rotating** makes a new key with the same name and scope; the old one keeps working for
  `ApiKeys:RotationGracePeriod` (a day), so programs can switch over.

How it works: the default authentication scheme looks at the bearer token. One that starts with
`sk_` goes to `ApiKeyAuthenticationHandler`, anything else to the JWT handler, so endpoints need
no changes: `[Authorize]` accepts either, and `user.GetUserId()` and the organisation claim are
the same. `ApiKeyRestrictions` (middleware) keeps keys to their paths and scopes.

## AI assistants (MCP)

`/mcp` is a [Model Context Protocol](https://modelcontextprotocol.io/) server (the official C#
SDK, Apache-2.0), so assistants such as Claude can use the organisation's Todos: `list_todos`,
`add_todo` and `complete_todo` (in `Integrations/Mcp/TodoTools.cs`). It only accepts API keys:

```json
{
  "mcpServers": {
    "todos": {
      "type": "http",
      "url": "https://your-domain/mcp",
      "headers": { "Authorization": "Bearer sk_..." }
    }
  }
}
```

The tools follow the same rules as the HTTP endpoints: validation, plan limits (the plan's error
comes back to the assistant as the tool's error), realtime updates and webhooks. The server is
stateless, so it works behind a load balancer.

**Adding a tool:** add a method with `[McpServerTool]` and a `[Description]` (the assistant reads
it to decide when to call the tool) to a class with `[McpServerToolType]`, and register the class
in `IntegrationsSetup` (`.WithTools<T>()`). Call `Caller.MustWrite()` first if the tool changes
anything. Open a session or DbContext for the key's organisation, as `TodoTools` does: the MCP SDK
runs tools outside Wolverine's endpoint pipeline.

## Outgoing webhooks

Owners and admins add a URL and pick events (or all of them). When an event happens, the app
POSTs JSON to the URL:

```json
{ "id": "...", "type": "todo.completed", "timestamp": "...", "data": { "id": "...", "title": "...", "isComplete": true } }
```

- **Signed the [Standard Webhooks](https://www.standardwebhooks.com/) way**, with the
  `webhook-id`, `webhook-timestamp` and `webhook-signature` headers. Receivers check the signature
  with the webhook's secret (shown when it's added, and on request) using a Standard Webhooks
  library for their language, or `WebhookSignatures.Check`. `webhook-id` is the same on every
  retry, so receivers can ignore repeats.
- **Sent from a durable Wolverine queue**, one message per delivery, so one slow receiver doesn't
  hold up the others. A receiver that fails (not a 2xx answer within `Webhooks:Timeout`, 10
  seconds) gets it again after 1, 5 and 30 minutes, 2 hours and 8 hours; then it's parked with
  the back office's failed messages.
- **Every delivery is logged** with the receiver's status and the start of its answer. Owners see
  them on the Integrations page, and can resend any of them or send a test event. Deliveries are
  kept for `Webhooks:KeepDeliveriesFor` (30 days).
- **Never to private networks in production:** the sender refuses localhost, private and
  link-local addresses (such as a cloud's metadata service at 169.254.169.254), checked on the
  address it actually connects to, after DNS. `Webhooks:AllowPrivateNetworks` turns this off, and
  allows plain HTTP, in Development only, to try a receiver on your machine. Redirects are never
  followed.

**Adding an event:** add a constant and its description to `WebhookEvents` (`Integrations/Webhooks/Webhook.cs`),
and publish `WebhookEvents.Raise(organisationId, type, data)` where it happens: return it with
the endpoint's or handler's other messages, so it goes through the outbox with the change that
caused it. `Features/Todos/CreateTodoEndpoint.cs` and `TodoCompleted.cs` do this.

## Secrets

Owners keep keys and tokens for other services here, such as their own OpenAI key or an OAuth
token, for the app to use on the organisation's behalf. Values are encrypted with ASP.NET Core
Data Protection (whose keys the app keeps in PostgreSQL), for that organisation only, and are
never shown again: the page shows names and the last four characters. The app's code reads them:

```csharp
var key = await secrets.GetAsync(organisationId, "openai-api-key", cancellationToken); // OrganisationSecrets
```

The same protection keeps webhook signing secrets. Back up the Data Protection keys (the
`mt_doc_dataprotectionkeydocument` table with Marten, `DataProtectionKeys` with EF Core) with the
database: without them, secrets can't be decrypted.

## Receiving webhooks

`POST /api/webhooks/incoming/{source}` receives webhooks from other services, the way billing
receives Stripe's: it checks the signature, stores the event once (a second delivery of it is
ignored), hands it to a durable message, and answers at once. The work happens in
`IncomingWebhookHandler` (`Integrations/Webhooks/IncomingWebhooks.cs`), which is retried if it
fails, so a slow handler never makes the sender give up.

For a provider that signs the Standard Webhooks way, no code is needed to receive: add its
signing secret as `Webhooks__Incoming__Secrets__<source>` (for example `..._chat`), point the
provider at `https://your-domain/api/webhooks/incoming/chat`, and handle
`IncomingWebhookReceived` for that source. For a provider that signs differently (GitHub, Slack),
copy the endpoint and change the signature check; `Features/Billing/PaymentEndpoints.cs` does
this for Stripe.

## Data and deletion

An organisation's keys, webhooks, deliveries and secrets are deleted with it, and its owners'
data download (the organisation page) lists its keys, webhooks and secrets without the keys,
signing secrets or values.
