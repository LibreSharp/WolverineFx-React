using System.Xml.Linq;
using Marten;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace ApiHost.Identity.Marten;

// One ASP.NET Core Data Protection key, stored as a Marten document.
public class DataProtectionKeyDocument
{
    public string Id { get; set; } = string.Empty;
    public string Xml { get; set; } = string.Empty;
}

// Stores Data Protection keys in Marten (there's no official package for this, unlike EF Core).
// IXmlRepository is synchronous but Marten 9 only supports asynchronous data access, so both
// methods wait for Marten's async calls. That's safe in ASP.NET Core (no synchronization context) and rare: keys are loaded
// at startup and a new one is created about every 90 days.
public class MartenDataProtectionKeyRepository(IDocumentStore store) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var session = store.QuerySession();
        var keys = session.Query<DataProtectionKeyDocument>().ToListAsync().GetAwaiter().GetResult();
        return keys.Select(key => XElement.Parse(key.Xml)).ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var session = store.LightweightSession();
        session.Store(new DataProtectionKeyDocument
        {
            Id = string.IsNullOrEmpty(friendlyName) ? Guid.NewGuid().ToString() : friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting)
        });
        session.SaveChangesAsync().GetAwaiter().GetResult();
    }
}
