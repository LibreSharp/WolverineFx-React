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

    [Fact]
    public async Task create_list_and_complete_a_todo()
    {
        var title = $"Write tests {Guid.NewGuid()}";

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.StatusCodeShouldBeSuccess();
        });

        var afterCreate = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/todos");
            s.StatusCodeShouldBeOk();
        });
        var todosAfterCreate = afterCreate.ReadAsJson<List<Todo>>();
        var created = Assert.Single(todosAfterCreate!, t => t.Title == title);
        Assert.False(created.IsComplete);

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/todos/{created.Id}/complete");
            s.StatusCodeShouldBeSuccess();
        });

        var afterComplete = await _fixture.Host.Scenario(s =>
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
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/todos/{Guid.NewGuid()}/complete");
            s.StatusCodeShouldBe(HttpStatusCode.NotFound);
        });
    }
}
