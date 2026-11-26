using System.Net;
using Alba;
using ApiHost.Features.Todos;

namespace ApiHost.Tests;

[Collection("api")]
public class TodoEndpointsTests
{
    private readonly AppFixture _fixture;

    public TodoEndpointsTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    // With Identity, the Todo endpoints require a bearer token; without it, token is null.
    private async Task<string?> GetTokenAsync()
    {
#if (Identity)
        return await _fixture.CreateUserAndGetAccessTokenAsync();
#else
        return await Task.FromResult<string?>(null);
#endif
    }

    private Task<IScenarioResult> Send(string? token, Action<Scenario> configure)
    {
        return _fixture.Host.Scenario(s =>
        {
            if (token is not null)
            {
                s.WithBearerToken(token);
            }
            configure(s);
        });
    }

    [Fact]
    public async Task create_list_and_complete_a_todo()
    {
        var token = await GetTokenAsync();
        var title = $"Write tests {Guid.NewGuid()}";

        await Send(token, s =>
        {
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });

        var afterCreate = await Send(token, s =>
        {
            s.Get.Url("/api/todos");
            s.StatusCodeShouldBeOk();
        });
        var todosAfterCreate = afterCreate.ReadAsJson<List<Todo>>();
        var created = Assert.Single(todosAfterCreate!, t => t.Title == title);
        Assert.False(created.IsComplete);

        await Send(token, s =>
        {
            s.Post.Url($"/api/todos/{created.Id}/complete");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });

        var afterComplete = await Send(token, s =>
        {
            s.Get.Url("/api/todos");
            s.StatusCodeShouldBeOk();
        });
        var todosAfterComplete = afterComplete.ReadAsJson<List<Todo>>();
        Assert.Contains(todosAfterComplete!, t => t.Id == created.Id && t.IsComplete);
    }

    [Fact]
    public async Task completing_an_unknown_todo_returns_not_found()
    {
        var token = await GetTokenAsync();

        await Send(token, s =>
        {
            s.Post.Url($"/api/todos/{Guid.NewGuid()}/complete");
            s.StatusCodeShouldBe(HttpStatusCode.NotFound);
        });
    }
}
