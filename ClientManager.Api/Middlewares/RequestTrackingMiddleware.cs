using System.Diagnostics;
using ClientManager.Api.Utils.Instrumentation;
using ClientManager.Shared.Logging;

namespace ClientManager.Api.Middlewares;

/// <summary>
/// Outermost custom middleware that captures request timing, logs request/response pairs,
/// and records OpenTelemetry metrics for every HTTP request.
/// <para>
/// Emits three instruments: <c>clientmanager.requests.total</c> (counter),
/// <c>clientmanager.requests.duration</c> (histogram), and <c>clientmanager.requests.errors</c>
/// (counter for status codes &gt;= 400). All instruments are tagged with method, endpoint,
/// and status code dimensions.
/// </para>
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

        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? context.TraceIdentifier;

        using var _ = NLog.ScopeContext.PushProperty("CorrelationId", correlationId);

        _logger.Debug("Request started", new { context.Request.Method, Path = context.Request.Path.Value, QueryString = context.Request.QueryString.Value });

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

            _logger.Info("Request completed", new { context.Request.Method, Path = context.Request.Path.Value, StatusCode = statusCode, DurationMs = stopwatch.Elapsed.TotalMilliseconds });
        }
    }
}
