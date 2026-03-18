using System.Diagnostics;
using ClientManager.Api.Services.Instrumentation;

namespace ClientManager.Api.Middleware;

/// <summary>
/// Outermost custom middleware that captures request timing, logs request/response pairs,
/// and records OpenTelemetry metrics for every HTTP request.
/// </summary>
public class RequestTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTrackingMiddleware> _logger;

    public RequestTrackingMiddleware(RequestDelegate next, ILogger<RequestTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ClientManagerMetrics metrics)
    {
        var stopwatch = Stopwatch.StartNew();
        var activity = Activity.Current;

        _logger.LogInformation("Request started | TraceId={TraceId}, Method={Method}, Path={Path}, QueryString={QueryString}",
            activity?.TraceId.ToString(), context.Request.Method, context.Request.Path.Value, context.Request.QueryString.Value);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;

            metrics.RequestsTotal.Add(1, new TagList
            {
                { "method", context.Request.Method },
                { "endpoint", context.Request.Path.Value },
                { "statusCode", statusCode.ToString() }
            });

            metrics.RequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
            {
                { "method", context.Request.Method },
                { "endpoint", context.Request.Path.Value }
            });

            if (statusCode >= 400)
            {
                metrics.RequestErrors.Add(1, new TagList
                {
                    { "method", context.Request.Method },
                    { "endpoint", context.Request.Path.Value },
                    { "statusCode", statusCode.ToString() }
                });
            }

            _logger.LogInformation("Request completed | TraceId={TraceId}, Method={Method}, Path={Path}, StatusCode={StatusCode}, DurationMs={DurationMs}",
                activity?.TraceId.ToString(), context.Request.Method, context.Request.Path.Value, statusCode, stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
