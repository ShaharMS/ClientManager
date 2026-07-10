namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Writes precomputed statistics documents during usage persistence.
/// </summary>
public interface IStatisticsPrecomputeService
{
    Task RefreshOverviewSummaryAsync(CancellationToken cancellationToken = default);

    Task RefreshLatestUsageGaugesAsync(CancellationToken cancellationToken = default);
}
