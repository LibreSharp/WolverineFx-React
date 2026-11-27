using ApiHost.DataExport;
using ApiHost.Integrations.ApiKeys;
using ApiHost.Integrations.Secrets;
using ApiHost.Integrations.Webhooks;

namespace ApiHost.Integrations;

// The organisation's API keys, webhooks and secrets, in its data download: what they are, never
// the keys, signing secrets or secret values themselves.
public class IntegrationsExportData(IApiKeyStore keys, IWebhookStore webhooks, OrganisationSecrets secrets) : IOrganisationDataSource
{
    public string Section => "integrations";

    public async Task<object?> ExportAsync(Guid organisationId, CancellationToken cancellationToken)
        => new
        {
            ApiKeys = (await keys.ListAsync(organisationId, cancellationToken)).Select(k => new { k.Id, k.Name, k.Hint, k.Scope, k.CreatedAt, k.ExpiresAt, k.LastUsedAt }),
            Webhooks = (await webhooks.SubscriptionsAsync(organisationId, cancellationToken)).Select(w => new { w.Id, w.Url, w.Description, w.Events, w.Enabled, w.CreatedAt }),
            Secrets = await secrets.ListAsync(organisationId, cancellationToken),
        };
}
