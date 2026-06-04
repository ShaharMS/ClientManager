using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Interfaces;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public metrics requests onto the in-process storage exporters, exposing only the exporter
/// payloads so the controller stays focused on HTTP content shaping.
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly IPrometheusExportService _prometheusExportService;
    private readonly IGrafanaExportService _grafanaExportService;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricsService"/>.
    /// </summary>
    /// <param name="prometheusExportService">In-process Prometheus exposition exporter.</param>
    /// <param name="grafanaExportService">In-process Grafana-shaped metrics exporter.</param>
    public MetricsService(
        IPrometheusExportService prometheusExportService,
        IGrafanaExportService grafanaExportService)
    {
        _prometheusExportService = prometheusExportService;
        _grafanaExportService = grafanaExportService;
    }

    /// <inheritdoc />
    public Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken = default) =>
        _prometheusExportService.ExportMetricsAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<GrafanaMetricsResponse> GetGrafanaMetricsAsync(CancellationToken cancellationToken = default) =>
        (GrafanaMetricsResponse)await _grafanaExportService.ExportMetricsAsync(cancellationToken);
}
