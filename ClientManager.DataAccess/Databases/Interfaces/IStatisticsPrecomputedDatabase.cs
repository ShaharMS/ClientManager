using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Persists precomputed statistics documents (overview summary, latest gauges).
/// </summary>
public interface IStatisticsPrecomputedDatabase
{
    Task<StatisticsOverviewSummary?> GetOverviewSummaryAsync(CancellationToken cancellationToken = default);

    Task UpsertOverviewSummaryAsync(StatisticsOverviewSummary summary, CancellationToken cancellationToken = default);

    Task<LatestUsageGaugesDocument?> GetLatestUsageGaugesAsync(CancellationToken cancellationToken = default);

    Task UpsertLatestUsageGaugesAsync(LatestUsageGaugesDocument document, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
