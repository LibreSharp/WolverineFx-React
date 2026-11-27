using System.Security;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;

namespace ApiHost.ClientApp;

// A page of the React app, from wwwroot/client-routes.json, which the client's build writes from
// src/Client/src/routes.ts. Index: listed in sitemap.xml for search engines.
public record ClientRoute(string Path, bool Index);

// Production only: serves the React app that `dotnet publish` builds into wwwroot. (In
// Development the Vite dev server serves it.)
public static class ClientAppHosting
{
    public static void MapClientApp(this WebApplication app)
    {
        var webRoot = app.Environment.WebRootFileProvider;
        var routes = ReadRoutes(webRoot, app.Logger);

        // Vite puts a content hash in every file name under /assets, so browsers and CDNs can cache
        // those for a year. (MapStaticAssets would only serve files known at build time, and the
        // client is copied in later, during publish.)
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                if (context.Context.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase))
                {
                    context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                }
            },
        });

        // For search engines: everything may be crawled except the API, so crawlers can see the
        // noindex tag on private pages (a page blocked here could still be listed, without it).
        app.MapGet("/robots.txt", (HttpContext context) => Results.Text(
            $"User-agent: *\nDisallow: /api/\n\nSitemap: {BaseUrl(context)}/sitemap.xml\n", "text/plain"));

        app.MapGet("/sitemap.xml", (HttpContext context) =>
        {
            var xml = new StringBuilder("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n')
                .Append("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""").Append('\n');
            foreach (var route in routes.Where(r => r.Index))
            {
                xml.Append("  <url><loc>").Append(SecurityElement.Escape(BaseUrl(context) + route.Path)).Append("</loc></url>\n");
            }

            return Results.Text(xml.Append("</urlset>\n").ToString(), "application/xml");
        });

        // Unknown API paths get a 404 (as ProblemDetails), never the app's HTML.
        app.MapFallback("/api/{**path}", () => Results.NotFound());

        // Every other path gets the app's HTML, so client-side routes such as /login load directly.
        // Paths that aren't pages get it with status 404 (the app shows its not-found page), so
        // search engines don't index them as duplicate pages. The HTML is never cached, so a new
        // deployment is picked up straight away.
        app.MapFallback(async context =>
        {
            var index = webRoot.GetFileInfo("index.html");
            context.Response.StatusCode = routes.Count == 0 || routes.Any(r => Matches(r.Path, context.Request.Path))
                ? StatusCodes.Status200OK
                : StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(index, context.RequestAborted);
        });
    }

    private static List<ClientRoute> ReadRoutes(IFileProvider webRoot, ILogger logger)
    {
        var file = webRoot.GetFileInfo("client-routes.json");
        if (!file.Exists)
        {
            logger.LogWarning("wwwroot/client-routes.json is missing: every path will get the app with status 200");
            return [];
        }

        using var stream = file.CreateReadStream();
        return JsonSerializer.Deserialize<List<ClientRoute>>(stream, JsonSerializerOptions.Web) ?? [];
    }

    // "/todos" matches "/todos" and "/todos/"; a ":name" segment matches any one segment.
    private static bool Matches(string routePath, PathString requestPath)
    {
        var route = routePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var request = (requestPath.Value ?? "/").Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return route.Length == request.Length
               && route.Zip(request).All(pair => pair.First.StartsWith(':') || string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
    }

    // The address the visitor used (behind a proxy, with ASPNETCORE_FORWARDEDHEADERS_ENABLED).
    private static string BaseUrl(HttpContext context)
        => $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
}
