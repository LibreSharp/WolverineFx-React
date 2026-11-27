using System.Net;
using Alba;
using ApiHost.Features.Todos;
#if (Saas)
using ApiHost.Organisations;
using Microsoft.IdentityModel.JsonWebTokens;
#endif
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace ApiHost.Tests;

// Marten only (excluded from EF Core projects by template.json): Todos are event-sourced,
// so their history is an event stream, not just the latest row.
[Collection("api")]
public class TodoEventStreamTests
{
    private readonly AppFixture _fixture;

    public TodoEventStreamTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task a_completed_todo_has_created_and_completed_events()
    {
#if (Identity)
        var token = await _fixture.CreateUserAndGetAccessTokenAsync();
#endif
        var title = $"Event sourced {Guid.NewGuid()}";

        var created = await _fixture.Host.Scenario(s =>
        {
#if (Identity)
            s.WithBearerToken(token);
#endif
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });
        var id = created.ReadAsJson<TodoCreationResponse>()!.Id;

        await _fixture.Host.Scenario(s =>
        {
#if (Identity)
            s.WithBearerToken(token);
#endif
            s.Post.Url($"/api/todos/{id}/complete");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });

#if (Saas)
        // The stream belongs to the user's organisation (the tenant in their access token).
        var organisationId = new JsonWebToken(token).GetClaim(OrganisationClaims.OrganisationId).Value;
        await using var session = _fixture.Host.Services.GetRequiredService<IDocumentStore>().QuerySession(organisationId);
#else
        await using var session = _fixture.Host.Services.GetRequiredService<IDocumentStore>().QuerySession();
#endif
        var events = await session.Events.FetchStreamAsync(id);

        Assert.Collection(events,
            e => Assert.Equal(title, Assert.IsType<TodoCreated>(e.Data).Title),
            e => Assert.Equal(id, Assert.IsType<TodoCompleted>(e.Data).TodoId));
    }
}
