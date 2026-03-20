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
    /// Retrieves usage over time for a specific service or resource pool.
    /// </summary>
    /// <param name="targetType">Whether the target is a Service or ResourcePool.</param>
    /// <param name="targetId">The ID of the service or resource pool.</param>
    /// <param name="clientIds">Optional client IDs to filter by.</param>
    /// <param name="from">Optional start of the time range (UTC).</param>
    /// <param name="to">Optional end of the time range (UTC).</param>
    /// <param name="granularity">Optional bucket granularity override.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Time-series data for usage and capacity.</returns>
    Task<UsageTimeSeriesResponse> GetUsageTimeSeriesAsync(
        GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves per-client usage breakdown for a specific service or resource pool.
    /// </summary>
    /// <param name="targetType">Whether the target is a Service or ResourcePool.</param>
    /// <param name="targetId">The ID of the service or resource pool.</param>
    /// <param name="clientIds">Optional client IDs to filter by.</param>
    /// <param name="from">Optional start of the time range (UTC).</param>
    /// <param name="to">Optional end of the time range (UTC).</param>
    /// <param name="granularity">Optional bucket granularity override.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Per-client usage breakdown.</returns>
    Task<ClientUsageBreakdownResponse> GetClientUsageBreakdownAsync(
        GlobalRateLimitTarget targetType, string targetId, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, BucketGranularity? granularity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a summary of all clients with their service and pool access statistics.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Client summary data for the dashboard table.</returns>
    Task<ClientSummariesResponse> GetClientSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical usage data for a specific target, optionally filtered by client.
    /// </summary>
    /// <param name="targetId">The ID of the service or resource pool.</param>
    /// <param name="targetType">Whether the target is a Service or ResourcePool.</param>
    /// <param name="clientId">Optional client ID to filter by.</param>
    /// <param name="from">Start of the time range (UTC).</param>
    /// <param name="to">End of the time range (UTC).</param>
    /// <param name="granularity">The bucket granularity to query.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Historical usage time-series data.</returns>
    Task<HistoricalUsageResponse> GetHistoricalUsageAsync(
        string targetId,
        GlobalRateLimitTarget targetType,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);
}
