using Alba;

namespace ApiHost.Tests;

[Collection("api")]
public class SecurityHeadersTests
{
    private readonly AppFixture _fixture;

    public SecurityHeadersTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task responses_carry_browser_security_headers()
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/health/live");
            s.StatusCodeShouldBeOk();
        });

        var headers = result.Context.Response.Headers;
        Assert.Contains("default-src 'self'", headers.ContentSecurityPolicy.ToString());
        Assert.Contains("frame-ancestors 'none'", headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("DENY", headers.XFrameOptions.ToString());
    }
}
