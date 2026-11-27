using ApiHost.DataExport;

namespace ApiHost.Billing;

// The organisation's plan, subscription and invoices, in its data download.
public class BillingExportData(IBillingStore store) : IOrganisationDataSource
{
    public string Section => "billing";

    public async Task<object?> ExportAsync(Guid organisationId, CancellationToken cancellationToken)
        => new
        {
            Account = await store.FindAccountAsync(organisationId, cancellationToken),
            Invoices = await store.InvoicesAsync(organisationId, cancellationToken),
        };
}
