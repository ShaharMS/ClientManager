using ClientManager.Api.Models.Responses;

namespace ClientManager.Api.Interfaces;

/// <summary>
/// Provides aggregated statistics for the dashboard including usage metrics,
/// time-series data, client breakdowns, and client summaries.
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// Retrieves global usage statistics including request rate and pool acquisition.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Global usage statistics.</returns>
    Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves usage over time for a specific service or resource pool.
    /// </summary>
    /// <param name="filterType">Either "Service" or "ResourcePool".</param>
    /// <param name="targetId">The ID of the service or resource pool.</param>
    /// <param name="clientIds">Optional client IDs to filter by.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Time-series data for usage and capacity.</returns>
    Task<UsageTimeSeriesResponse> GetUsageTimeSeriesAsync(
        string filterType, string targetId, IEnumerable<string>? clientIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves per-client usage breakdown for a specific service or resource pool.
    /// </summary>
    /// <param name="filterType">Either "Service" or "ResourcePool".</param>
    /// <param name="targetId">The ID of the service or resource pool.</param>
    /// <param name="clientIds">Optional client IDs to filter by.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Per-client usage breakdown.</returns>
    Task<ClientUsageBreakdownResponse> GetClientUsageBreakdownAsync(
        string filterType, string targetId, IEnumerable<string>? clientIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a summary of all clients with their service and pool access statistics.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Client summary data for the dashboard table.</returns>
    Task<ClientSummariesResponse> GetClientSummariesAsync(CancellationToken cancellationToken = default);
}
