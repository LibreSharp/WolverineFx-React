using Alba;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.PostgreSql;

namespace ApiHost.Tests;

public class AppFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16").Build();
        await _postgres.StartAsync();

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseEnvironment("Development");
#if (IsMarten)
            builder.UseSetting("ConnectionStrings:Marten", _postgres.GetConnectionString());
#elif (IsEfCore)
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
#endif
        });
    }

    public async Task DisposeAsync()
    {
        await Host.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<AppFixture>;
