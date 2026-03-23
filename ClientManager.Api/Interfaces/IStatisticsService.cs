using ClientManager.Api.Models.Responses;
using ClientManager.Shared.Models.Enums;

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
    /// Retrieves usage over time for one or more services or resource pools.
    /// </summary>
    /// <param name="targetType">Whether the targets are Services or ResourcePools.</param>
    /// <param name="targetIds">The IDs of the services or resource pools.</param>
    /// <param name="clientIds">Optional client IDs to filter by.</param>
    /// <param name="from">Optional start of the time range (UTC).</param>
    /// <param name="to">Optional end of the time range (UTC).</param>
    /// <param name="granularity">Optional bucket granularity override.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Per-target time-series data for usage and capacity.</returns>
    Task<List<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType targetType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves per-client usage breakdown for one or more services or resource pools.
    /// </summary>
    /// <param name="targetType">Whether the targets are Services or ResourcePools.</param>
    /// <param name="targetIds">The IDs of the services or resource pools.</param>
    /// <param name="clientIds">Optional client IDs to filter by.</param>
    /// <param name="from">Optional start of the time range (UTC).</param>
    /// <param name="to">Optional end of the time range (UTC).</param>
    /// <param name="granularity">Optional bucket granularity override.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Per-target client usage breakdowns.</returns>
    Task<List<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        TargetType targetType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a summary of all clients with their service and pool access statistics.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Client summary data for the dashboard table.</returns>
    Task<ClientSummariesResponse> GetClientSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical usage data for one or more targets, optionally filtered by client.
    /// </summary>
    /// <param name="targetIds">The IDs of the services or resource pools.</param>
    /// <param name="targetType">Whether the targets are Services or ResourcePools.</param>
    /// <param name="clientId">Optional client ID to filter by.</param>
    /// <param name="from">Start of the time range (UTC).</param>
    /// <param name="to">End of the time range (UTC).</param>
    /// <param name="granularity">The bucket granularity to query.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Historical usage time-series data per target.</returns>
    Task<List<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        IEnumerable<string> targetIds,
        TargetType targetType,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);
}
