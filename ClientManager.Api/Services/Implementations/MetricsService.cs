using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public metrics requests onto the storage-facing <see cref="IStatisticsReadClient"/>,
/// exposing only the exporter payloads so the controller stays focused on HTTP content shaping.
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly IStatisticsReadClient _statisticsReadClient;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricsService"/>.
    /// </summary>
    /// <param name="statisticsReadClient">Typed client for the storage-facing metrics endpoints.</param>
    public MetricsService(IStatisticsReadClient statisticsReadClient)
    {
        _statisticsReadClient = statisticsReadClient;
    }

    /// <inheritdoc />
    public Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetPrometheusMetricsAsync(cancellationToken);

    /// <inheritdoc />
    public Task<GrafanaMetricsResponse> GetGrafanaMetricsAsync(CancellationToken cancellationToken = default) =>
        _statisticsReadClient.GetGrafanaMetricsAsync(cancellationToken);
}
