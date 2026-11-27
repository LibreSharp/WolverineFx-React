using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApiHost.Security;
using Microsoft.Extensions.FileProviders;

namespace ApiHost.ClientApp;

// A page of the React app, from wwwroot/client-routes.json, which the client's build writes from
// src/Client/src/pages.ts. Index: public, so the build pre-rendered it and sitemap.xml lists it.
public record ClientRoute(string Path, bool Index);

// An HTML file of the React app, with the Content-Security-Policy that allows its inline scripts.
internal sealed record HtmlPage(IFileInfo File, string ContentSecurityPolicy);

// Production only: serves the React app that `dotnet publish` builds into wwwroot. (In
// Development the Vite dev server serves it.)
public static partial class ClientAppHosting
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

        app.MapSearchEngineFiles(routes);

        // Unknown API paths get a 404 (as ProblemDetails), never the app's HTML.
        app.MapFallback("/api/{**path}", () => Results.NotFound());

        // Every other path gets the app's HTML, so client-side routes such as /login load directly:
        // the page's own HTML if the build pre-rendered it (the public pages, such as /), otherwise
        // the fallback page that starts the app. Paths that aren't pages get the fallback with
        // status 404 (the app shows its not-found page), so search engines don't index them as
        // duplicate pages. Browsers revalidate the HTML on every visit (no-cache), so a new
        // deployment is picked up straight away; see docs/how-to/put-cloudflare-in-front.md for
        // caching the public pages at a CDN.
        var fallback = ReadPage(webRoot.GetFileInfo("__spa-fallback.html") is { Exists: true } spaFallback
            ? spaFallback
            : webRoot.GetFileInfo("index.html"));
        var prerendered = routes
            .Select(route => (route.Path, File: PrerenderedFile(webRoot, route.Path)))
            .Where(page => page.File is not null)
            .ToDictionary(page => page.Path, page => ReadPage(page.File!), StringComparer.Ordinal);
        app.MapFallback(async context =>
        {
            var route = routes.FirstOrDefault(r => Matches(r.Path, context.Request.Path));
            var page = route is not null && prerendered.TryGetValue(route.Path, out var own) ? own : fallback;
            context.Response.StatusCode = routes.Count == 0 || route is not null
                ? StatusCodes.Status200OK
                : StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.ContentSecurityPolicy = page.ContentSecurityPolicy;
            await context.Response.SendFileAsync(page.File, context.RequestAborted);
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

    // robots.txt and sitemap.xml, using the address the visitor used.
    private static void MapSearchEngineFiles(this WebApplication app, List<ClientRoute> routes)
    {
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
    }

    // Hashes the page's inline scripts once, at startup: the files don't change while the app runs.
    private static HtmlPage ReadPage(IFileInfo file)
    {
        if (!file.Exists)
        {
            return new HtmlPage(file, SecurityHeaders.ContentSecurityPolicyAllowing([]));
        }

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        var html = reader.ReadToEnd();
        var hashes = InlineScript().Matches(html)
            .Where(match => !match.Groups["attributes"].Value.Contains("src=", StringComparison.OrdinalIgnoreCase))
            .Select(match => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(match.Groups["code"].Value))))
            .Distinct(StringComparer.Ordinal);
        return new HtmlPage(file, SecurityHeaders.ContentSecurityPolicyAllowing(hashes));
    }

    [GeneratedRegex("<script(?<attributes>[^>]*)>(?<code>.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineScript();

    // The build writes a pre-rendered page to index.html (for /) or <path>/index.html.
    private static IFileInfo? PrerenderedFile(IFileProvider webRoot, string routePath)
    {
        if (routePath.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        var file = webRoot.GetFileInfo(routePath == "/" ? "index.html" : $"{routePath.Trim('/')}/index.html");
        return file.Exists ? file : null;
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
