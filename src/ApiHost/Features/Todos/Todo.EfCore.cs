#if (Saas)
using System.Text.Json.Serialization;
using JasperFx.MultiTenancy;

#endif
namespace ApiHost.Features.Todos;

// A row in the Todos table (Data/EfCore/AppDbContext.cs).
#if (Saas)
// ITenanted: each Todo belongs to an organisation (the tenant). Wolverine and
// EF Core set and filter TenantId, so the app never does.
public class Todo : ITenanted
#else
public class Todo
#endif
{
    public Guid Id { get; set; }
#if (Saas)

    [JsonIgnore]
    public string? TenantId { get; set; }

#endif
#if (Identity)
    public string OwnerId { get; set; } = string.Empty;
#endif
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}
