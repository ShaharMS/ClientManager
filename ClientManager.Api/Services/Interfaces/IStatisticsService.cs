using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Provides read-only dashboard statistics.
/// </summary>
/// <remarks>
/// <para>
/// The overview endpoint serves the Admin UI dashboard cards with aggregate client and service counts
/// plus a live requests-per-minute gauge. RPM values come from the shared in-storage second-bucket ring
/// (five-minute average) so every replica reports the same number operators see in the UI.
/// </para>
/// <para>
/// External observability stacks can derive RPM from cumulative request counters and PromQL rate queries.
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
