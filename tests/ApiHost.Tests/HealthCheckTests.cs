using Alba;

namespace ApiHost.Tests;

[Collection("api")]
public class HealthCheckTests
{
    private readonly AppFixture _fixture;

    public HealthCheckTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task health_endpoints_report_healthy_without_logging_in(string url)
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url(url);
            s.StatusCodeShouldBeOk();
        });

        Assert.Equal("Healthy", await result.ReadAsTextAsync());
    }
}
