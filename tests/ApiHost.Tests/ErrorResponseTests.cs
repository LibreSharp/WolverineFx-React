using System.Net;
using System.Text.Json;
using Alba;

namespace ApiHost.Tests;

[Collection("api")]
public class ErrorResponseTests
{
    private readonly AppFixture _fixture;

    public ErrorResponseTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task an_unknown_api_route_answers_404_problem_details_with_a_trace_id()
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/does-not-exist");
            s.StatusCodeShouldBe(HttpStatusCode.NotFound);
        });

        Assert.StartsWith("application/problem+json", result.Context.Response.ContentType, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(await result.ReadAsTextAsync());
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }
}
