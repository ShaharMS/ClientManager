using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Provides system metrics in the exposition formats expected by external monitoring platforms.
/// Surfaces the raw Prometheus payload and the Grafana-shaped response while keeping the public
/// controller decoupled from the storage transport that produces them.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Gets system metrics in Prometheus exposition format.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The Prometheus-formatted metrics text.</returns>
    Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets system metrics in the Grafana-shaped JSON format.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The Grafana metrics response.</returns>
    Task<GrafanaMetricsResponse> GetGrafanaMetricsAsync(CancellationToken cancellationToken = default);
}
