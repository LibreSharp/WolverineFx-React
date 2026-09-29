using System.Net;
using Alba;
using ApiHost.Features.Todos;

namespace ApiHost.Tests;

[Collection("api")]
public class TodoAuthorizationTests
{
    private readonly AppFixture _fixture;

    public TodoAuthorizationTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<Todo> CreateTodoAsync(string token)
    {
        var title = $"Private todo {Guid.NewGuid()}";

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.WithBearerToken(token);
            s.StatusCodeShouldBeSuccess();
        });

        var todos = await ListTodosAsync(token);
        return Assert.Single(todos, t => t.Title == title);
    }

    private async Task<List<Todo>> ListTodosAsync(string token)
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/todos");
            s.WithBearerToken(token);
            s.StatusCodeShouldBeOk();
        });
        return result.ReadAsJson<List<Todo>>()!;
    }

    [Fact]
    public async Task anonymous_list_is_unauthorized()
    {
        await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/todos");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }

    [Fact]
    public async Task anonymous_create_is_unauthorized()
    {
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateTodo("anonymous")).ToUrl("/api/todos");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }

    [Fact]
    public async Task anonymous_complete_is_unauthorized()
    {
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/todos/{Guid.NewGuid()}/complete");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }

    [Fact]
    public async Task a_user_cannot_see_another_users_todos()
    {
        var ownerToken = await _fixture.CreateUserAndGetAccessTokenAsync();
        var otherToken = await _fixture.CreateUserAndGetAccessTokenAsync();
        var todo = await CreateTodoAsync(ownerToken);

        var othersTodos = await ListTodosAsync(otherToken);

        Assert.DoesNotContain(othersTodos, t => t.Id == todo.Id);
    }

    [Fact]
    public async Task a_user_cannot_complete_another_users_todo()
    {
        var ownerToken = await _fixture.CreateUserAndGetAccessTokenAsync();
        var otherToken = await _fixture.CreateUserAndGetAccessTokenAsync();
        var todo = await CreateTodoAsync(ownerToken);

        // Reported as missing rather than forbidden, so ids can't be probed for existence.
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/todos/{todo.Id}/complete");
            s.WithBearerToken(otherToken);
            s.StatusCodeShouldBe(HttpStatusCode.NotFound);
        });

        var ownersTodos = await ListTodosAsync(ownerToken);
        Assert.Contains(ownersTodos, t => t.Id == todo.Id && !t.IsComplete);
    }
}
