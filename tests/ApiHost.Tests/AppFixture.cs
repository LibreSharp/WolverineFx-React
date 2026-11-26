#if (Identity)
using System.Collections.Concurrent;
using System.Net;
using ApiHost.Identity.Email;
using ApiHost.Identity.Endpoints;
using Microsoft.Extensions.DependencyInjection;
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
#if (Identity)
            // Keep account emails (password reset) so tests can follow their links.
            builder.ConfigureServices(services => services.AddSingleton<IAccountEmailSender>(Emails));
#endif
#if (IsMarten)
            builder.UseSetting("ConnectionStrings:Marten", _postgres.GetConnectionString());
#elif (IsEfCore)
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
#endif
        });
    }

#if (Identity)
    public CapturedEmails Emails { get; } = new();

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

#if (Identity)
// Records password reset emails instead of sending them.
public class CapturedEmails : IAccountEmailSender
{
    private readonly ConcurrentDictionary<string, string> _resetLinks = new(StringComparer.OrdinalIgnoreCase);

    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken)
    {
        _resetLinks[toEmail] = resetLink;
        return Task.CompletedTask;
    }

    public string? ResetLinkFor(string email) => _resetLinks.GetValueOrDefault(email);
}

#endif
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<AppFixture>;
