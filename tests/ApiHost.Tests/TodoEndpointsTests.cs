using System.Net;
using Alba;
using ApiHost.Features.Todos;
using Wolverine.Tracking;

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
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task creating_a_todo_without_a_title_is_a_validation_error(string title)
    {
        var token = await GetTokenAsync();

        var result = await Send(token, s =>
        {
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        Assert.Contains("Title", await result.ReadAsTextAsync());
    }

    [Fact]
    public async Task creating_a_todo_with_a_too_long_title_is_a_validation_error()
    {
        var token = await GetTokenAsync();

        await Send(token, s =>
        {
            s.Post.Json(new CreateTodo(new string('x', CreateTodoValidator.MaxTitleLength + 1))).ToUrl("/api/todos");
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task completing_a_todo_publishes_TodoCompleted_to_a_background_handler()
    {
        var token = await GetTokenAsync();
        var title = $"Publish me {Guid.NewGuid()}";

        await Send(token, s =>
        {
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });
        var todos = (await Send(token, s => s.Get.Url("/api/todos"))).ReadAsJson<List<Todo>>();
        var created = Assert.Single(todos!, t => t.Title == title);

        // Waits until every message the request caused has been handled.
        var tracked = await _fixture.Host.ExecuteAndWaitAsync(() => Send(token, s =>
        {
            s.Post.Url($"/api/todos/{created.Id}/complete");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        }));

        // Filter by id: tests share one app, so another test's TodoCompleted can be
        // handled during this tracking window too.
        var message = Assert.Single(tracked.Executed.MessagesOf<TodoCompleted>(), m => m.TodoId == created.Id);
        Assert.Equal(title, message.Title);

        // Completing again is a no-op: no second message.
        var again = await _fixture.Host.ExecuteAndWaitAsync(() => Send(token, s =>
        {
            s.Post.Url($"/api/todos/{created.Id}/complete");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        }));
        Assert.DoesNotContain(again.Executed.MessagesOf<TodoCompleted>(), m => m.TodoId == created.Id);
    }
}
