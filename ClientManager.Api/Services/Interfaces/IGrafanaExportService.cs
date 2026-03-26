namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Formats usage statistics in OpenMetrics JSON format for Grafana consumption.
/// <para>
/// Reads from usage snapshots and allocation repositories to produce a structured
/// JSON response containing metric definitions with labels. The output is designed
/// for Grafana's JSON API data source plugin.
/// </para>
/// </summary>
public interface IGrafanaExportService
{
    /// <summary>
    /// Generates OpenMetrics JSON format metrics including per-service request/denied
    /// counters, global requests-per-minute, and per-pool slot utilization.
    /// </summary>
    /// <param name="cancellationToken">Cancels the metrics generation and any database queries.</param>
    /// <returns>JSON-formatted metrics object.</returns>
    Task<object> ExportMetricsAsync(CancellationToken cancellationToken = default);
}
