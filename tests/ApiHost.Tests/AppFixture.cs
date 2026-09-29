#if (Identity)
using System.Net;
using ApiHost.Identity.Endpoints;
#endif
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

#if (Identity)
    // Registers a fresh user and returns an access token for them.
    public async Task<string> CreateUserAndGetAccessTokenAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        const string password = "P@ssword123!";

        await Host.Scenario(s =>
        {
            s.Post.Json(new RegisterRequest(email, password)).ToUrl("/api/auth/register");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });

        var login = await Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        });

        return login.ReadAsJson<LoginResponse>()!.AccessToken;
    }

#endif
    public async Task DisposeAsync()
    {
        await Host.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<AppFixture>;
