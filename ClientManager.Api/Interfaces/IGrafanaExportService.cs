namespace ClientManager.Api.Interfaces;

/// <summary>
/// Formats usage statistics in OpenMetrics JSON format for Grafana consumption.
/// </summary>
public interface IGrafanaExportService
{
    /// <summary>
    /// Generates OpenMetrics JSON format metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON-formatted metrics object.</returns>
    Task<object> ExportMetricsAsync(CancellationToken cancellationToken = default);
}
