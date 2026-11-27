namespace ApiHost.Identity;

public static class ClientUrl
{
    public const string Setting = "App:ClientUrl";

    // Where the React app is served, for links in emails and the redirect after external sign-in:
    // App:ClientUrl. Never the request's Host header, which the caller chooses: a password reset
    // requested with "Host: evil.example" would email the victim a genuine link to the attacker's
    // site, carrying the reset token. Outside Development the app won't start without the setting
    // (Validate); in Development it falls back to this API's own address.
    public static string For(IConfiguration configuration, HttpContext httpContext)
        => configuration[Setting]?.TrimEnd('/')
           ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";

    // Called at startup: outside Development, App:ClientUrl must be the app's absolute http(s)
    // address, such as https://app.example.com.
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        var value = configuration[Setting];
        if (string.IsNullOrWhiteSpace(value))
        {
            if (environment.IsDevelopment())
            {
                return;
            }

            throw new InvalidOperationException(
                $"'{Setting}' must be set to the app's public address, such as https://app.example.com: " +
                "links in emails use it. Set it with the 'App__ClientUrl' environment variable.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"'{Setting}' must be an absolute http(s) address, such as https://app.example.com, not '{value}'.");
        }
    }
}
