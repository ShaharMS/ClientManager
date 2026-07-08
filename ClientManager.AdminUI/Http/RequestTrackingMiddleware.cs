using System.Diagnostics;
using ClientManager.Shared.Logging;

namespace ClientManager.AdminUI.Http;

/// <summary>
/// Logs HTTP request duration for correlating browser TTFB with server-side handling.
/// </summary>
public sealed class RequestTrackingMiddleware
{
    private const double SlowRequestThresholdMs = 250;
    private readonly RequestDelegate _next;
    private readonly IAppLogger<RequestTrackingMiddleware> _logger;

    public RequestTrackingMiddleware(
        RequestDelegate next,
        IAppLogger<RequestTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        // ponytail: Blazor/SignalR circuits are long-lived; wall-clock duration is not a slow-request signal.
        if (ShouldSkipRequestTracking(context, path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        _logger.Debug("Request started", new
        {
            Method = context.Request.Method,
            Path = path,
            QueryString = context.Request.QueryString.Value
        });

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            var payload = new
            {
                Method = context.Request.Method,
                Path = path,
                StatusCode = context.Response.StatusCode,
                DurationMs = Math.Round(durationMs, 2)
            };

            if (durationMs >= SlowRequestThresholdMs)
            {
                _logger.Warn("Request completed slowly", payload);
            }
            else
            {
                _logger.Debug("Request completed", payload);
            }
        }
    }

    private static bool ShouldSkipRequestTracking(HttpContext context, string path) =>
        path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
        || context.WebSockets.IsWebSocketRequest
        || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/fonts", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/bootstrap", StringComparison.OrdinalIgnoreCase);
}
