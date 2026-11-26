using ApiHost.Features.Todos;
#if (Identity)
using ApiHost.Identity;
#endif
using JasperFx.Events.Projections;
using JasperFx.OpenTelemetry;
using Marten;
using Wolverine.Marten;

namespace ApiHost.Data.Marten;

public static class MartenPersistence
{
    public static void AddMartenPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMarten(opts =>
        {
            opts.Connection(configuration.GetConnectionString("Marten")
                ?? throw new InvalidOperationException("Missing 'Marten' connection string"));
            opts.UseSystemTextJsonForSerialization();

            // Trace Marten's database work (exported by OpenTelemetry; see Observability/).
            opts.OpenTelemetry.TrackConnections = TrackLevel.Normal;

            // Todos are event-sourced. The inline snapshot keeps a Todo document up to date
            // in the same transaction as each appended event, so queries read documents
            // instead of replaying streams.
            opts.Projections.Snapshot<Todo>(SnapshotLifecycle.Inline);

#if (Identity)
            opts.Schema.For<RefreshToken>()
                .Index(t => t.TokenHash, x => x.IsUnique = true)
                .Index(t => t.UserId);

            opts.Schema.For<Todo>()
                .Index(t => t.OwnerId);
#endif
        }).IntegrateWithWolverine();
    }
}
