using System.Diagnostics;
using ClientManager.Api.Utils.Instrumentation;
using ClientManager.Shared.Logging;

namespace ClientManager.Api.Middlewares;

/// <summary>
/// Outermost custom middleware that captures request timing, logs request/response pairs,
/// and records OpenTelemetry metrics for every HTTP request.
/// <para>
/// Emits three instruments: <c>clientmanager.http.requests</c> (counter),
/// <c>clientmanager.http.requests.duration</c> (histogram), and <c>clientmanager.http.requests.errors</c>
/// (counter for status codes &gt;= 400). All instruments are tagged with method, endpoint,
/// and status code dimensions.
/// </para>
/// </summary>
public class RequestTrackingMiddleware(
    RequestDelegate next,
    IAppLogger<RequestTrackingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ClientManagerMetrics metrics)
    {
        var stopwatch = Stopwatch.StartNew();

        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? context.TraceIdentifier;

        using var _ = NLog.ScopeContext.PushProperty("CorrelationId", correlationId);

        logger.Debug("Request started", new { context.Request.Method, Path = context.Request.Path.Value, QueryString = context.Request.QueryString.Value });

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var requestTags = CreateRequestTags(context, includeStatusCode: true);

            metrics.HttpRequestsTotal.Add(1, requestTags);
            metrics.RequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, CreateRequestTags(context));

            if (statusCode >= 400)
            {
                metrics.RequestErrors.Add(1, requestTags);
            }

            logger.Info("Request completed", new { context.Request.Method, Path = context.Request.Path.Value, StatusCode = statusCode, DurationMs = stopwatch.Elapsed.TotalMilliseconds });
        }
    }

    private static TagList CreateRequestTags(HttpContext context, bool includeStatusCode = false)
    {
        var tags = new TagList
        {
            { "method", context.Request.Method },
            { "endpoint", context.Request.Path.Value }
        };

        if (includeStatusCode)
        {
            tags.Add("statusCode", context.Response.StatusCode.ToString());
        }

        return tags;
    }
}
