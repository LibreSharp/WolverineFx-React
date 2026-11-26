namespace ApiHost.Security;

public static class SecurityHeaders
{
    // Browser security headers on every response. The Content-Security-Policy only lets the page
    // load scripts, styles, images and connections (API calls, the SignalR WebSocket) from this
    // app's own origin, which blocks most injected-script attacks. If you add a third-party
    // script, font or analytics service, add its origin to the matching directive.
    // (In Development the page comes from the Vite dev server, so these don't affect it.)
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; " +
        "form-action 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            // The Development-only API reference (/scalar) runs its own inline setup script.
            if (!context.Request.Path.StartsWithSegments("/scalar"))
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            await next(context);
        });
    }
}
