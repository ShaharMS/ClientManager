using System.Text.Json;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Internal.Interfaces;

/// <summary>
/// Typed read-only client for the storage-facing statistics and metrics endpoints.
/// Aggregates system overviews, per-target summaries and detail documents, usage time series,
/// and exporter payloads so statistics controllers stay decoupled from the storage API transport.
/// </summary>
public interface IStatisticsReadClient
{
    /// <summary>Gets the high-level system overview.</summary>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The system overview snapshot.</returns>
    Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken);

    /// <summary>Searches per-client summary statistics matching the supplied query.</summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching client summaries and total hit count.</returns>
    Task<SearchResult<ClientSummaryResponse>> SearchClientSummariesAsync(DocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Gets the raw detail document for a single client.</summary>
    /// <param name="clientId">The client identifier to inspect.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The client detail document as raw JSON.</returns>
    Task<JsonElement> GetClientDetailsAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>Searches per-service statistics matching the supplied query.</summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching service statistics and total hit count.</returns>
    Task<SearchResult<ServiceStatisticsResponse>> SearchServiceStatisticsAsync(DocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Gets the raw detail document for a single service.</summary>
    /// <param name="serviceId">The service identifier to inspect.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The service detail document as raw JSON.</returns>
    Task<JsonElement> GetServiceDetailsAsync(string serviceId, CancellationToken cancellationToken);

    /// <summary>Searches per-resource-pool statistics matching the supplied query.</summary>
    /// <param name="query">The search criteria, paging, and sort options.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The matching resource-pool statistics and total hit count.</returns>
    Task<SearchResult<ResourcePoolStatisticsResponse>> SearchResourcePoolStatisticsAsync(DocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Gets the raw detail document for a single resource pool.</summary>
    /// <param name="resourcePoolId">The resource-pool identifier to inspect.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The resource-pool detail document as raw JSON.</returns>
    Task<JsonElement> GetResourcePoolDetailsAsync(string resourcePoolId, CancellationToken cancellationToken);

    /// <summary>Gets aggregate global usage statistics across all targets.</summary>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The global usage statistics snapshot.</returns>
    Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(CancellationToken cancellationToken);

    /// <summary>Gets usage time series for the requested targets and window.</summary>
    /// <param name="filterType">The kind of target the identifiers refer to.</param>
    /// <param name="targetIds">The target identifiers to include.</param>
    /// <param name="clientIds">Optional client identifiers to scope the series to.</param>
    /// <param name="from">Optional inclusive start of the window.</param>
    /// <param name="to">Optional inclusive end of the window.</param>
    /// <param name="granularity">Optional bucket granularity for the series.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The per-target usage time series.</returns>
    Task<IReadOnlyList<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken);

    /// <summary>Gets the per-client usage breakdown for the requested targets and window.</summary>
    /// <param name="filterType">The kind of target the identifiers refer to.</param>
    /// <param name="targetIds">The target identifiers to include.</param>
    /// <param name="clientIds">Optional client identifiers to scope the breakdown to.</param>
    /// <param name="from">Optional inclusive start of the window.</param>
    /// <param name="to">Optional inclusive end of the window.</param>
    /// <param name="granularity">Optional bucket granularity for the breakdown.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The per-target, per-client usage breakdown.</returns>
    Task<IReadOnlyList<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken);

    /// <summary>Gets summary statistics for all clients.</summary>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The collected client summaries.</returns>
    Task<ClientSummariesResponse> GetClientSummariesAsync(CancellationToken cancellationToken);

    /// <summary>Gets historical usage for the requested targets and window.</summary>
    /// <param name="filterType">The kind of target the identifiers refer to.</param>
    /// <param name="targetIds">The target identifiers to include.</param>
    /// <param name="clientId">Optional client identifier to scope the history to.</param>
    /// <param name="from">Inclusive start of the window.</param>
    /// <param name="to">Inclusive end of the window.</param>
    /// <param name="granularity">Bucket granularity for the history.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The per-target historical usage.</returns>
    Task<IReadOnlyList<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken);

    /// <summary>Gets historical usage broken down by client for the requested targets and window.</summary>
    /// <param name="filterType">The kind of target the identifiers refer to.</param>
    /// <param name="targetIds">The target identifiers to include.</param>
    /// <param name="clientIds">The client identifiers to break the history down by.</param>
    /// <param name="from">Inclusive start of the window.</param>
    /// <param name="to">Inclusive end of the window.</param>
    /// <param name="granularity">Bucket granularity for the history.</param>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The per-target, per-client historical usage.</returns>
    Task<IReadOnlyList<ClientHistoricalUsageResponse>> GetHistoricalUsageByClientAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        IEnumerable<string> clientIds,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken);

    /// <summary>Gets the raw Prometheus exposition payload from the storage API.</summary>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The Prometheus metrics text.</returns>
    Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken);

    /// <summary>Gets the Grafana-shaped metrics payload from the storage API.</summary>
    /// <param name="cancellationToken">Token used to cancel the storage call.</param>
    /// <returns>The Grafana metrics response.</returns>
    Task<GrafanaMetricsResponse> GetGrafanaMetricsAsync(CancellationToken cancellationToken);
}
