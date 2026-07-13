using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Provides read-only dashboard statistics.
/// </summary>
/// <remarks>
/// <para>
/// Statistics are intentionally lightweight: the Admin UI dashboard only needs aggregate counts and
/// a live RPM gauge, not historical timeseries payloads. RPM values come from the shared in-storage
/// second-bucket ring (five-minute average) so every replica reports the same number operators see
/// internally.
/// </para>
/// <para>
/// Prometheus/Grafana consumers should use cumulative request counters and PromQL rate queries
/// rather than this endpoint.
/// </para>
/// </remarks>
public interface IStatisticsService
{
    /// <summary>
    /// Returns system overview counts and the current requests-per-minute gauge.
    /// </summary>
    /// <param name="cancellationToken">Cancels the overview query before it completes.</param>
    /// <returns>Client count, service count, and current RPM.</returns>
    Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);
}
