using System.Diagnostics;
using ClientManager.Shared.Logging;

namespace ClientManager.AdminUI.Http;

/// <summary>
/// Logs outbound API calls made by the named <c>ClientManagerApi</c> HTTP client.
/// </summary>
public sealed class OutboundHttpLoggingHandler(IAppLogger<OutboundHttpLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = request.Method.Method;
        var path = request.RequestUri?.PathAndQuery;

        logger.Debug("Outbound request started", new { Method = method, Path = path });

        HttpResponseMessage? response = null;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
            return response;
        }
        finally
        {
            stopwatch.Stop();
            LogCompleted(method, path, response, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private void LogCompleted(string method, string? path, HttpResponseMessage? response, double durationMs)
    {
        if (response is null)
        {
            logger.Warn("Outbound request failed", new { Method = method, Path = path, DurationMs = durationMs });
            return;
        }

        logger.Info("Outbound request completed", new
        {
            Method = method,
            Path = path,
            StatusCode = (int)response.StatusCode,
            DurationMs = durationMs
        });
    }
}
