#if (Identity)
using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using ApiHost.Identity.Email;
using ApiHost.Identity.Endpoints;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;
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
        _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await _postgres.StartAsync();

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseEnvironment("Development");
#if (Identity)
            // Keep account emails instead of sending them, so tests can follow their links.
            builder.ConfigureServices(services => services.AddSingleton<IEmailSender>(Emails));
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

    // Registers a user and confirms their email with the link from the confirmation email, as a
    // person would, so they can log in.
    public async Task RegisterConfirmedUserAsync(string email, string password)
    {
        // Waits for the confirmation email, which is sent in the background.
        await Host.ExecuteAndWaitAsync(() => Host.Scenario(s =>
        {
            s.Post.Json(new RegisterRequest(email, password)).ToUrl("/api/auth/register");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        }));

        var query = Emails.LinkQuery(email, "/confirm-email");
        await Host.Scenario(s =>
        {
            s.Post.Json(new ConfirmEmailRequest(query["userId"]!, query["token"]!)).ToUrl("/api/auth/confirm-email");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
    }

    // Registers a fresh, confirmed user and returns an access token for them.
    public async Task<string> CreateUserAndGetAccessTokenAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        const string password = "P@ssword123!";

        await RegisterConfirmedUserAsync(email, password);

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
// Records emails instead of sending them.
public partial class CapturedEmails : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> SentTo(string email)
        => _sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).ToList();

    // The most recent link to the given client page (such as "/reset-password") emailed to this
    // address, or null if there is none.
    public Uri? LinkFor(string email, string page)
        => SentTo(email)
            .SelectMany(m => LinkPattern().Matches(m.TextBody).Select(match => new Uri(match.Value)))
            .LastOrDefault(uri => uri.AbsolutePath == page);

    // The query string of that link (for example its token), failing the test if there's no link.
    public IDictionary<string, Microsoft.Extensions.Primitives.StringValues> LinkQuery(string email, string page)
        => QueryHelpers.ParseQuery((LinkFor(email, page) ?? throw new InvalidOperationException($"No {page} link was emailed to {email}")).Query);

    [GeneratedRegex(@"https?://\S+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinkPattern();
}

#endif
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<AppFixture>;
