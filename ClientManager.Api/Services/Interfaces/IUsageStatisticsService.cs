namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Exports usage metrics in Prometheus exposition format.
/// </summary>
public interface IPrometheusExportService
{
    Task<string> ExportMetricsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Exports usage metrics as Grafana JSON payloads.
/// </summary>
public interface IGrafanaExportService
{
    Task<object> ExportMetricsAsync(CancellationToken cancellationToken = default);
}
