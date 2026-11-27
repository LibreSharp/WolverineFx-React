namespace ApiHost.Identity;

public static class ClientUrl
{
    // Where the React app is served, for links in emails: App:ClientUrl if set (Development, where
    // the Vite dev server runs on another port), otherwise this API's own address (production,
    // where the API serves the app).
    public static string For(IConfiguration configuration, HttpContext httpContext)
        => configuration["App:ClientUrl"]?.TrimEnd('/')
           ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";
}
