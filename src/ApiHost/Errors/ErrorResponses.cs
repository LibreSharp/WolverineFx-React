using System.Diagnostics;

namespace ApiHost.Errors;

// Every error response is ProblemDetails JSON (RFC 9457), which the client already reads:
// - an unhandled exception becomes a 500 with a generic title (in production, no exception
//   details: those are in the logs);
// - an error status with no body (404 for an unknown route, 401, 403, 429...) gets one.
// Each carries a traceId, the same ID as the request's trace and log entries, so a user's report
// can be matched to the logs.
public static class ErrorResponses
{
    public static void AddErrorResponses(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
    }

    public static void UseErrorResponses(this WebApplication app)
    {
        // In Development, ASP.NET Core's developer exception page (with the stack trace) takes
        // precedence; it also answers API calls with ProblemDetails.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler();
        }

        app.UseStatusCodePages();
    }
}
