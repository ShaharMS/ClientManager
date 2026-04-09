using System.Text.Json;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Interfaces;

public interface IStatisticsReadClient
{
    Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken);

    Task<SearchResult<ClientSummaryResponse>> SearchClientSummariesAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<JsonElement> GetClientDetailsAsync(string clientId, CancellationToken cancellationToken);

    Task<SearchResult<ServiceStatisticsResponse>> SearchServiceStatisticsAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<JsonElement> GetServiceDetailsAsync(string serviceId, CancellationToken cancellationToken);

    Task<SearchResult<ResourcePoolStatisticsResponse>> SearchResourcePoolStatisticsAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<JsonElement> GetResourcePoolDetailsAsync(string resourcePoolId, CancellationToken cancellationToken);

    Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken);

    Task<ClientSummariesResponse> GetClientSummariesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken);

    Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken);

    Task<GrafanaMetricsResponse> GetGrafanaMetricsAsync(CancellationToken cancellationToken);
}