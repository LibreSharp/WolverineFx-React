using System.Net;
using System.Text.Json;
using Alba;
using ApiHost.Features.Auth;
using ApiHost.Features.Integrations;
using ApiHost.Features.Organisations;
using ApiHost.Features.Todos;
using ApiHost.Identity;
using ApiHost.Integrations;
using ApiHost.Integrations.ApiKeys;
using ApiHost.Integrations.Mcp;
using ApiHost.Organisations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ApiHost.Tests;

// --Auth saas only (excluded otherwise by template.json): API keys and the MCP endpoint.
[Collection("api")]
public class IntegrationTests(AppFixture fixture)
{
    private const string Password = "P@ssword123!";

    private sealed record Session(string UserId, LoginResponse Tokens)
    {
        public string AccessToken => Tokens.AccessToken;

        public Guid OrganisationId => Guid.Parse(new JsonWebToken(AccessToken).GetClaim(OrganisationClaims.OrganisationId).Value);
    }

    private async Task<Session> SignUpAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await fixture.RegisterConfirmedUserAsync(email, Password);
        var tokens = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, Password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LoginResponse>()!;
        return new Session(new JsonWebToken(tokens.AccessToken).Subject, tokens);
    }

    private async Task<CreatedApiKey> CreateKeyAsync(Session session, string scope = ApiKeyScopes.Write, string name = "CI")
        => (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateApiKeyRequest(name, scope, null)).ToUrl("/api/organisation/api-keys");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<CreatedApiKey>()!;

    private Task<IScenarioResult> WithKeyAsync(string key, HttpMethod method, string url, object? body, HttpStatusCode expected)
        => fixture.Host.Scenario(s =>
        {
            if (method == HttpMethod.Post)
            {
                s.Post.Json(body ?? new { }).ToUrl(url);
            }
            else
            {
                s.Get.Url(url);
            }

            s.WithRequestHeader("Authorization", $"Bearer {key}");
            s.StatusCodeShouldBe(expected);
        });

    private async Task<McpClient> McpClientAsync(string authorization)
    {
        var http = fixture.Host.Server.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, IntegrationsSetup.McpPath),
                AdditionalHeaders = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = authorization },
            },
            http,
            ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    private static T ResultOf<T>(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        var json = result.StructuredContent is { } structured
            ? structured.ToString()
            : Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web)!;
    }

    private static string ErrorOf(CallToolResult result)
    {
        Assert.True(result.IsError);
        return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    }

    [Fact]
    public async Task a_key_works_like_its_creator_in_its_organisation_and_is_only_shown_once()
    {
        var owner = await SignUpAsync();
        var created = await CreateKeyAsync(owner);
        Assert.StartsWith(ApiKeySecrets.Prefix, created.Key, StringComparison.Ordinal);
        Assert.StartsWith(created.ApiKey.Hint, created.Key, StringComparison.Ordinal);

        await WithKeyAsync(created.Key, HttpMethod.Post, "/api/todos", new CreateTodo("From a script"), HttpStatusCode.Created);
        var todos = (await WithKeyAsync(created.Key, HttpMethod.Get, "/api/todos", null, HttpStatusCode.OK)).ReadAsJson<List<Todo>>()!;
        Assert.Equal("From a script", Assert.Single(todos).Title);

        // The list never shows the key, only its start; and it records when it was used.
        var listed = (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisation/api-keys");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBeOk();
        }));
        Assert.DoesNotContain(created.Key, listed.ReadAsText(), StringComparison.Ordinal);
        Assert.NotNull(Assert.Single(listed.ReadAsJson<List<ApiKeySummary>>()!).LastUsedAt);

        // Another organisation's data stays out of reach.
        var other = await SignUpAsync();
        var otherKey = await CreateKeyAsync(other);
        Assert.Empty((await WithKeyAsync(otherKey.Key, HttpMethod.Get, "/api/todos", null, HttpStatusCode.OK)).ReadAsJson<List<Todo>>()!);
    }

    [Fact]
    public async Task keys_only_reach_the_paths_they_are_for_and_read_keys_only_read()
    {
        var owner = await SignUpAsync();
        var read = await CreateKeyAsync(owner, ApiKeyScopes.Read);

        await WithKeyAsync(read.Key, HttpMethod.Get, "/api/todos", null, HttpStatusCode.OK);
        await WithKeyAsync(read.Key, HttpMethod.Post, "/api/todos", new CreateTodo("Nope"), HttpStatusCode.Forbidden);

        // A key can't manage the account, the organisation or other keys.
        await WithKeyAsync(read.Key, HttpMethod.Get, "/api/organisation", null, HttpStatusCode.Forbidden);
        await WithKeyAsync(read.Key, HttpMethod.Get, "/api/organisation/api-keys", null, HttpStatusCode.Forbidden);
        await WithKeyAsync(read.Key, HttpMethod.Get, "/api/account/me", null, HttpStatusCode.Forbidden);

        // A made-up key is refused.
        await WithKeyAsync(ApiKeySecrets.Generate(), HttpMethod.Get, "/api/todos", null, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task revoking_a_key_stops_it_at_once_and_rotating_keeps_the_old_one_for_a_while()
    {
        var owner = await SignUpAsync();
        var first = await CreateKeyAsync(owner, name: "Deploys");

        var rotated = (await fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/organisation/api-keys/{first.ApiKey.Id}/rotate");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<CreatedApiKey>()!;
        Assert.Equal("Deploys", rotated.ApiKey.Name);
        Assert.NotEqual(first.Key, rotated.Key, StringComparer.Ordinal);

        // Both work during the grace period; the old one now has an end.
        await WithKeyAsync(first.Key, HttpMethod.Get, "/api/todos", null, HttpStatusCode.OK);
        await WithKeyAsync(rotated.Key, HttpMethod.Get, "/api/todos", null, HttpStatusCode.OK);
        var keys = (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisation/api-keys");
            s.WithBearerToken(owner.AccessToken);
        })).ReadAsJson<List<ApiKeySummary>>()!;
        Assert.NotNull(keys.Single(k => k.Id == first.ApiKey.Id).ExpiresAt);

        await fixture.Host.Scenario(s =>
        {
            s.Delete.Url($"/api/organisation/api-keys/{rotated.ApiKey.Id}");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        await WithKeyAsync(rotated.Key, HttpMethod.Get, "/api/todos", null, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task members_cannot_manage_keys_and_a_key_stops_when_its_creator_leaves()
    {
        var owner = await SignUpAsync();
        var admin = await SignUpAsync();
        var member = await SignUpAsync();
        using (var scope = fixture.Host.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOrganisationStore>();
            foreach (var (session, role) in new[] { (admin, OrganisationRoles.Admin), (member, OrganisationRoles.Member) })
            {
                await store.AddMembershipAsync(new Membership
                {
                    Id = Guid.CreateVersion7(),
                    OrganisationId = owner.OrganisationId,
                    UserId = session.UserId,
                    Role = role,
                    JoinedAt = DateTimeOffset.UtcNow,
                }, CancellationToken.None);
            }
        }

        var adminSession = await SwitchAsync(admin, owner.OrganisationId);
        var memberSession = await SwitchAsync(member, owner.OrganisationId);
        Assert.Equal(owner.OrganisationId, adminSession.OrganisationId);

        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateApiKeyRequest("Mine", ApiKeyScopes.Read, null)).ToUrl("/api/organisation/api-keys");
            s.WithBearerToken(memberSession.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Forbidden);
        });

        var key = await CreateKeyAsync(adminSession);
        await WithKeyAsync(key.Key, HttpMethod.Get, "/api/todos", null, HttpStatusCode.OK);

        await fixture.Host.Scenario(s =>
        {
            s.Delete.Url($"/api/organisation/members/{admin.UserId}");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        await WithKeyAsync(key.Key, HttpMethod.Get, "/api/todos", null, HttpStatusCode.Unauthorized);
    }

    private async Task<Session> SwitchAsync(Session session, Guid organisationId)
    {
        var tokens = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new SwitchOrganisationRequest(organisationId, session.Tokens.RefreshToken)).ToUrl("/api/organisations/switch");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LoginResponse>()!;
        return session with { Tokens = tokens };
    }

    [Fact]
    public async Task an_assistant_lists_adds_and_completes_todos_through_mcp()
    {
        var owner = await SignUpAsync();
        var key = await CreateKeyAsync(owner);
        await using var client = await McpClientAsync($"Bearer {key.Key}");

        var tools = await client.ListToolsAsync();
        Assert.Equal(["add_todo", "complete_todo", "list_todos"], tools.Select(t => t.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);

        var added = ResultOf<TodoSummary>(await client.CallToolAsync("add_todo", new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "Asked by an assistant" }));
        Assert.False(added.IsComplete);

        var completed = ResultOf<TodoSummary>(await client.CallToolAsync("complete_todo", new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = added.Id }));
        Assert.True(completed.IsComplete);

        var listed = ResultOf<List<TodoSummary>>(await client.CallToolAsync("list_todos"));
        Assert.True(Assert.Single(listed).IsComplete);

        // The same Todo through the HTTP API, as the organisation's members see it.
        var todos = (await WithKeyAsync(key.Key, HttpMethod.Get, "/api/todos", null, HttpStatusCode.OK)).ReadAsJson<List<Todo>>()!;
        Assert.Equal("Asked by an assistant", Assert.Single(todos).Title);

        Assert.Contains("200", ErrorOf(await client.CallToolAsync("add_todo", new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = new string('x', 201) })), StringComparison.Ordinal);
    }

    [Fact]
    public async Task owners_download_the_organisation_data_without_any_keys_or_secrets()
    {
        var owner = await SignUpAsync();
        var key = await CreateKeyAsync(owner);
        await WithKeyAsync(key.Key, HttpMethod.Post, "/api/todos", new CreateTodo("In the download"), HttpStatusCode.Created);

        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisation/export");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBeOk();
        });
        var text = result.ReadAsText();
        Assert.DoesNotContain(key.Key, text, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretHash", text, StringComparison.OrdinalIgnoreCase);

        using var file = JsonDocument.Parse(text);
        Assert.Contains("In the download", file.RootElement.GetProperty("todos").ToString(), StringComparison.Ordinal);
        Assert.Single(file.RootElement.GetProperty("organisation").GetProperty("members").EnumerateArray());
        Assert.Equal("CI", Assert.Single(file.RootElement.GetProperty("integrations").GetProperty("apiKeys").EnumerateArray()).GetProperty("name").GetString());
        Assert.True(file.RootElement.TryGetProperty("billing", out _));
    }

    [Fact]
    public async Task mcp_needs_an_api_key_and_read_keys_cannot_change_anything()
    {
        var owner = await SignUpAsync();

        // A user's login isn't accepted at /mcp.
        await Assert.ThrowsAnyAsync<Exception>(() => McpClientAsync($"Bearer {owner.AccessToken}"));

        var read = await CreateKeyAsync(owner, ApiKeyScopes.Read);
        await using var client = await McpClientAsync($"Bearer {read.Key}");
        Assert.Empty(ResultOf<List<TodoSummary>>(await client.CallToolAsync("list_todos")));
        Assert.Contains("only read", ErrorOf(await client.CallToolAsync("add_todo", new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "Nope" })), StringComparison.Ordinal);
    }
}
