using System.Diagnostics;
using ClientManager.Api.Services.Instrumentation;
using ClientManager.Shared.Logging;

namespace ClientManager.Api.Middleware;

/// <summary>
/// Outermost custom middleware that captures request timing, logs request/response pairs,
/// and records OpenTelemetry metrics for every HTTP request.
/// </summary>
public class RequestTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAppLogger<RequestTrackingMiddleware> _logger;

    public RequestTrackingMiddleware(RequestDelegate next, IAppLogger<RequestTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ClientManagerMetrics metrics)
    {
        var stopwatch = Stopwatch.StartNew();
        var activity = Activity.Current;

        _logger.Debug("Request started", new { TraceId = activity?.TraceId.ToString(), Method = context.Request.Method, Path = context.Request.Path.Value, QueryString = context.Request.QueryString.Value });

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

            _logger.Info("Request completed", new { TraceId = activity?.TraceId.ToString(), Method = context.Request.Method, Path = context.Request.Path.Value, StatusCode = statusCode, DurationMs = stopwatch.Elapsed.TotalMilliseconds });
        }
    }
}
