using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Writes precomputed statistics documents during usage persistence.
/// </summary>
public interface IStatisticsPrecomputeService
{
    Task RefreshOverviewSummaryAsync(CancellationToken cancellationToken = default);

    Task UpdateLatestUsageGaugesAsync(
        IReadOnlyCollection<ServiceClientGaugeKey> dirtyPairs,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Identifies a service-client pair whose gauge row should be refreshed.
/// </summary>
public readonly record struct ServiceClientGaugeKey(string ServiceId, string ClientId);
