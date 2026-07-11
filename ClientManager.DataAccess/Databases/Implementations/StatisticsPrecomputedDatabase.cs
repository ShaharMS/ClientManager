using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Stores precomputed statistics meta documents in the statistics document store.
/// </summary>
public sealed class StatisticsPrecomputedDatabase : IStatisticsPrecomputedDatabase
{
    private const string Collection = "StatisticsMeta";

    private readonly IDocumentStore _store;

    public StatisticsPrecomputedDatabase(IDocumentStore store) => _store = store;

    public Task<StatisticsOverviewSummary?> GetOverviewSummaryAsync(CancellationToken cancellationToken = default) =>
        _store.GetAsync<StatisticsOverviewSummary>(Collection, StatisticsOverviewSummary.DocumentId, cancellationToken);

    public Task UpsertOverviewSummaryAsync(StatisticsOverviewSummary summary, CancellationToken cancellationToken = default) =>
        _store.SetAsync(Collection, StatisticsOverviewSummary.DocumentId, summary, cancellationToken);

    public Task<LatestUsageGaugesDocument?> GetLatestUsageGaugesAsync(CancellationToken cancellationToken = default) =>
        _store.GetAsync<LatestUsageGaugesDocument>(Collection, LatestUsageGaugesDocument.DocumentId, cancellationToken);

    public Task UpsertLatestUsageGaugesAsync(LatestUsageGaugesDocument document, CancellationToken cancellationToken = default) =>
        _store.SetAsync(Collection, LatestUsageGaugesDocument.DocumentId, document, cancellationToken);
}
