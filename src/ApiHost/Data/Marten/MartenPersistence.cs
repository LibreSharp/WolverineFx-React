#if (Identity)
using ApiHost.Features.Todos;
using ApiHost.Identity;
#endif
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
