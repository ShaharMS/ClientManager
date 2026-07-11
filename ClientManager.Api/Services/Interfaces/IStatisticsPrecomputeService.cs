using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Writes precomputed statistics documents during usage persistence.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RefreshOverviewSummaryAsync"/> runs on the slow rollup loop when snapshot history changes.
/// </para>
/// <para>
/// <see cref="UpdateLatestUsageGaugesAsync"/> runs on the fast flush loop for only the service×client
/// pairs touched in that flush, avoiding a full gauge sweep on every second.
/// </para>
/// </remarks>
public interface IStatisticsPrecomputeService
{
    /// <summary>
    /// Recomputes dashboard overview fields (RPM, pool acquisition) from recent snapshots and live allocations.
    /// </summary>
    /// <remarks>Called from the slow <see cref="Storage.UsageTracking.UsagePersistenceService"/> loop after rollup or prune.</remarks>
    Task RefreshOverviewSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementally updates Prometheus/Grafana gauge rows for the given service×client pairs.
    /// </summary>
    /// <param name="dirtyPairs">
    /// Pairs whose counters changed in the latest fast-loop flush. Empty collections are ignored.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Reads pending counters once per call, prefers the latest second in the five-minute overlay window,
    /// and falls back to the newest second-level snapshot bucket when no pending counters exist.
    /// </remarks>
    Task UpdateLatestUsageGaugesAsync(
        IReadOnlyCollection<ServiceClientGaugeKey> dirtyPairs,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Identifies a service×client pair whose latest-usage gauge row should be refreshed.
/// </summary>
/// <param name="ServiceId">Service target identifier.</param>
/// <param name="ClientId">Client identifier.</param>
public readonly record struct ServiceClientGaugeKey(string ServiceId, string ClientId);
