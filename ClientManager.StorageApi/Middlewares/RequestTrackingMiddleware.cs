using System.Diagnostics;
using ClientManager.Shared.Logging;
using ClientManager.StorageApi.Utils.Instrumentation;

namespace ClientManager.StorageApi.Middlewares;

/// <summary>
/// Captures request timing, logs request and response pairs, and emits request metrics.
/// </summary>
public class RequestTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAppLogger<RequestTrackingMiddleware> _logger;

    public RequestTrackingMiddleware(
        RequestDelegate next,
        IAppLogger<RequestTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, StorageApiMetrics metrics)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.TraceIdentifier;

        using var _ = NLog.ScopeContext.PushProperty("CorrelationId", correlationId);

        _logger.Debug("Request started", new
        {
            context.Request.Method,
            Path = context.Request.Path.Value,
            QueryString = context.Request.QueryString.Value
        });

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

            _logger.Info("Request completed", new
            {
                context.Request.Method,
                Path = context.Request.Path.Value,
                StatusCode = statusCode,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }
    }
}