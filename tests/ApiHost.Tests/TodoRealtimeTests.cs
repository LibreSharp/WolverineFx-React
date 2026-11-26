using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Alba;
using ApiHost.Features.Todos;
using ApiHost.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace ApiHost.Tests;

// Connects real SignalR clients to the in-memory test server and checks that Todo changes
// are pushed to them (long polling, because the test server doesn't do WebSockets).
[Collection("api")]
public class TodoRealtimeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly AppFixture _fixture;

    public TodoRealtimeTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class Listener(HubConnection connection, Channel<JsonElement> messages) : IAsyncDisposable
    {
        public HubConnection Connection => connection;

        // Waits for the next "todo-changed" CloudEvent about this Todo, ignoring others.
        public async Task<JsonElement> NextChangeForAsync(Guid todoId, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (true)
            {
                var message = await messages.Reader.ReadAsync(cts.Token);
                if (message.GetProperty("type").GetString() == "todo-changed"
                    && message.GetProperty("data").GetProperty("todoId").GetGuid() == todoId)
                {
                    return message;
                }
            }
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private async Task<Listener> ConnectAsync(string? token)
    {
        var server = _fixture.Host.Server;
        var messages = Channel.CreateUnbounded<JsonElement>();

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, AppHub.Path.TrimStart('/')), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                if (token is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .Build();

        // Wolverine sends each message as CloudEvents JSON text.
        connection.On<string>("ReceiveMessage", json => messages.Writer.TryWrite(JsonDocument.Parse(json).RootElement.Clone()));

        await connection.StartAsync();
        return new Listener(connection, messages);
    }

    private Task<string?> GetTokenAsync()
    {
#if (Identity)
        return _fixture.CreateUserAndGetAccessTokenAsync()!;
#else
        return Task.FromResult<string?>(null);
#endif
    }

    private async Task<Guid> CreateTodoAsync(string? token, string title)
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            if (token is not null)
            {
                s.WithBearerToken(token);
            }
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });
        return result.ReadAsJson<TodoCreationResponse>()!.Id;
    }

    [Fact]
    public async Task creating_and_completing_a_todo_notifies_connected_clients()
    {
        var token = await GetTokenAsync();
        await using var tab = await ConnectAsync(token);

        var id = await CreateTodoAsync(token, $"Realtime {Guid.NewGuid()}");
        await tab.NextChangeForAsync(id, Timeout);

        // Completion reaches the browser via the background TodoCompletedHandler.
        await _fixture.Host.Scenario(s =>
        {
            if (token is not null)
            {
                s.WithBearerToken(token);
            }
            s.Post.Url($"/api/todos/{id}/complete");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        await tab.NextChangeForAsync(id, Timeout);
    }
#if (Identity)

    [Fact]
    public async Task every_tab_of_the_owner_is_notified_but_other_users_are_not()
    {
        var owner = await GetTokenAsync();
        var someoneElse = await GetTokenAsync();
        await using var ownerTab1 = await ConnectAsync(owner);
        await using var ownerTab2 = await ConnectAsync(owner);
        await using var otherUsersTab = await ConnectAsync(someoneElse);

        var id = await CreateTodoAsync(owner, $"Private realtime {Guid.NewGuid()}");

        await ownerTab1.NextChangeForAsync(id, Timeout);
        await ownerTab2.NextChangeForAsync(id, Timeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => otherUsersTab.NextChangeForAsync(id, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task anonymous_clients_cannot_connect_to_the_hub()
    {
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => ConnectAsync(token: null));
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }
#endif
}
