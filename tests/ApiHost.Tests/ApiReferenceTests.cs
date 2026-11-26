using Alba;

namespace ApiHost.Tests;

// The fixture runs the app in Development, where the OpenAPI document and the Scalar API
// reference are available.
[Collection("api")]
public class ApiReferenceTests
{
    private readonly AppFixture _fixture;

    public ApiReferenceTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task the_openapi_document_describes_the_todo_endpoints()
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/openapi/v1.json");
            s.StatusCodeShouldBeOk();
        });

        Assert.Contains("/api/todos", await result.ReadAsTextAsync());
    }

    [Fact]
    public async Task the_api_reference_page_is_served_with_its_own_scripts()
    {
        var page = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/scalar/");
            s.StatusCodeShouldBeOk();
        });
        Assert.Contains("scalar.js", await page.ReadAsTextAsync());

        // Served by the app itself, not a CDN.
        await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/scalar/scalar.js");
            s.StatusCodeShouldBeOk();
        });
    }
}
