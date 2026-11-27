using ApiHost.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ApiHost.Tests;

// Links in emails use App:ClientUrl, never the request's Host header (which the caller chooses),
// and the app refuses to start without it outside Development.
public class ClientUrlTests
{
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "ApiHost";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Configuration(string? clientUrl)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { [ClientUrl.Setting] = clientUrl }).Build();

    [Fact]
    public void links_use_the_configured_address_whatever_the_host_header_says()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("evil.example");

        Assert.Equal("https://app.example.com", ClientUrl.For(Configuration("https://app.example.com/"), context));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("https://app.example.com/?next=evil")]
    public void outside_development_the_app_refuses_to_start_without_a_valid_address(string? clientUrl)
        => Assert.Throws<InvalidOperationException>(() => ClientUrl.Validate(Configuration(clientUrl), new Environment(Environments.Production)));

    [Fact]
    public void a_valid_address_or_development_without_one_is_accepted()
    {
        ClientUrl.Validate(Configuration("https://app.example.com"), new Environment(Environments.Production));
        ClientUrl.Validate(Configuration(null), new Environment(Environments.Development));
    }
}
