namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Formats usage statistics in Prometheus exposition format.
/// <para>
/// Reads from usage snapshots and allocation repositories to produce a
/// <c>text/plain; version=0.0.4</c> response suitable for Prometheus scraping.
/// </para>
/// </summary>
public interface IPrometheusExportService
{
    /// <summary>
    /// Generates Prometheus exposition format metrics text including per-service
    /// request/denied counters, global requests-per-minute, and per-pool slot utilization.
    /// </summary>
    /// <param name="cancellationToken">Cancels the metrics generation and any database queries.</param>
    /// <returns>Prometheus-formatted metrics string.</returns>
    Task<string> ExportMetricsAsync(CancellationToken cancellationToken = default);
}
