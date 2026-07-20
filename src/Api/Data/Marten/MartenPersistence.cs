using Marten;
using Wolverine.Marten;

namespace Api.Data.Marten;

public static class MartenPersistence
{
    public static void AddMartenPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMarten(opts =>
        {
            opts.Connection(configuration.GetConnectionString("Marten")
                ?? throw new InvalidOperationException("Missing 'Marten' connection string"));
            opts.UseSystemTextJsonForSerialization();
        }).IntegrateWithWolverine();
    }
}
