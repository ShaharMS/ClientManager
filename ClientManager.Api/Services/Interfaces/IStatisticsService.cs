using System.Text.Json;
using ClientManager.Shared.Contracts.Statistics;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Provides read-only statistics about system state: overviews, per-target summaries and detail
/// documents, usage time series, and historical breakdowns.
/// <para>
/// The service owns the request-shaping helpers the controller would otherwise carry, normalizing
/// optional <see cref="IdentifierList"/> filters and paginating client-summary rows so the
/// controller only binds inputs and shapes the HTTP response.
/// </para>
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// Gets a high-level system overview with counts of clients, services, pools, and allocations.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The system overview snapshot.</returns>
    Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches per-client summary statistics using the supplied query.
    /// </summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The matching client summaries and total hit count.</returns>
    Task<SearchResult<ClientSummaryResponse>> SearchClientsAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the detailed statistics document for a single client.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The client detail document as raw JSON.</returns>
    Task<JsonElement> GetClientDetailsAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches per-service usage statistics using the supplied query.
    /// </summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The matching service statistics and total hit count.</returns>
    Task<SearchResult<ServiceStatisticsResponse>> SearchServicesAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the detailed statistics document for a single service.
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The service detail document as raw JSON.</returns>
    Task<JsonElement> GetServiceDetailsAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches per-resource-pool utilization statistics using the supplied query.
    /// </summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The matching resource pool statistics and total hit count.</returns>
    Task<SearchResult<ResourcePoolStatisticsResponse>> SearchResourcePoolsAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the detailed statistics document for a single resource pool.
    /// </summary>
    /// <param name="resourcePoolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The resource pool detail document as raw JSON.</returns>
    Task<JsonElement> GetResourcePoolDetailsAsync(string resourcePoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregate global usage statistics across all targets.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The global usage statistics snapshot.</returns>
    Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage time series for the requested targets and window.
    /// </summary>
    /// <param name="filterType">The kind of target the identifiers refer to.</param>
    /// <param name="targetIds">The target identifiers to include.</param>
    /// <param name="clientIds">Optional client identifiers to scope the series to; empty is treated as unscoped.</param>
    /// <param name="from">Optional inclusive start of the window.</param>
    /// <param name="to">Optional inclusive end of the window.</param>
    /// <param name="granularity">Optional bucket granularity for the series.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The per-target usage time series.</returns>
    Task<IReadOnlyList<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType filterType,
        IdentifierList targetIds,
        IdentifierList? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the per-client usage breakdown for the requested targets and window.
    /// </summary>
    /// <param name="filterType">The kind of target the identifiers refer to.</param>
    /// <param name="targetIds">The target identifiers to include.</param>
    /// <param name="clientIds">Optional client identifiers to scope the breakdown to; empty is treated as unscoped.</param>
    /// <param name="from">Optional inclusive start of the window.</param>
    /// <param name="to">Optional inclusive end of the window.</param>
    /// <param name="granularity">Optional bucket granularity for the breakdown.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The per-target, per-client usage breakdown.</returns>
    Task<IReadOnlyList<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        TargetType filterType,
        IdentifierList targetIds,
        IdentifierList? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated summary of all clients with their service and pool access statistics.
    /// </summary>
    /// <param name="paging">The requested page and page size.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The requested page of client summary rows.</returns>
    Task<PagedResponse<ClientSummaryRow>> GetClientSummariesAsync(PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical usage for the requested targets and window.
    /// </summary>
    /// <param name="filterType">The kind of target the identifiers refer to.</param>
    /// <param name="targetIds">The target identifiers to include.</param>
    /// <param name="clientId">Optional client identifier to scope the history to.</param>
    /// <param name="from">Inclusive start of the window.</param>
    /// <param name="to">Inclusive end of the window.</param>
    /// <param name="granularity">Bucket granularity for the history.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The per-target historical usage.</returns>
    Task<IReadOnlyList<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        TargetType filterType,
        IdentifierList targetIds,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical usage broken down by client for the requested targets and window.
    /// </summary>
    /// <param name="filterType">The kind of target the identifiers refer to.</param>
    /// <param name="targetIds">The target identifiers to include.</param>
    /// <param name="clientIds">The client identifiers to break the history down by.</param>
    /// <param name="from">Inclusive start of the window.</param>
    /// <param name="to">Inclusive end of the window.</param>
    /// <param name="granularity">Bucket granularity for the history.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The per-target, per-client historical usage.</returns>
    Task<IReadOnlyList<ClientHistoricalUsageResponse>> GetHistoricalUsageByClientAsync(
        TargetType filterType,
        IdentifierList targetIds,
        IdentifierList clientIds,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken = default);
}
