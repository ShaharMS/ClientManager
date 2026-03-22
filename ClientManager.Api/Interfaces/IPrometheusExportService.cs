namespace ClientManager.Api.Interfaces;

/// <summary>
/// Formats usage statistics in Prometheus exposition format.
/// </summary>
public interface IPrometheusExportService
{
    /// <summary>
    /// Generates Prometheus exposition format metrics text.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Prometheus-formatted metrics string.</returns>
    Task<string> ExportMetricsAsync(CancellationToken cancellationToken = default);
}
