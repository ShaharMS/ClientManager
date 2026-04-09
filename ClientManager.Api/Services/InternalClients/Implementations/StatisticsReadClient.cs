using System.Net.Http.Json;
using System.Text.Json;
using ClientManager.Api.Services.InternalClients.Interfaces;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Implementations;

internal sealed class StatisticsReadClient(HttpClient httpClient) : IStatisticsReadClient
{
    public async Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.Statistics.Overview, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SystemOverviewResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The storage API returned an empty overview response.");
    }

    public Task<SearchResult<ClientSummaryResponse>> SearchClientSummariesAsync(
        DocumentQuery query,
        CancellationToken cancellationToken)
    {
        return SearchAsync<ClientSummaryResponse>(StorageApiRoutes.Statistics.SearchClientSummaries, query, cancellationToken);
    }

    public Task<JsonElement> GetClientDetailsAsync(string clientId, CancellationToken cancellationToken)
    {
        return GetJsonAsync(StorageApiRoutes.Statistics.ClientDetails(clientId), cancellationToken);
    }

    public Task<SearchResult<ServiceStatisticsResponse>> SearchServiceStatisticsAsync(
        DocumentQuery query,
        CancellationToken cancellationToken)
    {
        return SearchAsync<ServiceStatisticsResponse>(StorageApiRoutes.Statistics.SearchServiceStatistics, query, cancellationToken);
    }

    public Task<JsonElement> GetServiceDetailsAsync(string serviceId, CancellationToken cancellationToken)
    {
        return GetJsonAsync(StorageApiRoutes.Statistics.ServiceDetails(serviceId), cancellationToken);
    }

    public Task<SearchResult<ResourcePoolStatisticsResponse>> SearchResourcePoolStatisticsAsync(
        DocumentQuery query,
        CancellationToken cancellationToken)
    {
        return SearchAsync<ResourcePoolStatisticsResponse>(
            StorageApiRoutes.Statistics.SearchResourcePoolStatistics,
            query,
            cancellationToken);
    }

    public Task<JsonElement> GetResourcePoolDetailsAsync(string resourcePoolId, CancellationToken cancellationToken)
    {
        return GetJsonAsync(StorageApiRoutes.Statistics.ResourcePoolDetails(resourcePoolId), cancellationToken);
    }

    public Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(CancellationToken cancellationToken)
    {
        return GetAsync<GlobalUsageStatsResponse>(StorageApiRoutes.Statistics.GlobalUsage, cancellationToken);
    }

    public Task<IReadOnlyList<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken)
    {
        return GetAsync<IReadOnlyList<TargetUsageTimeSeriesResponse>>(
            StorageApiRoutes.Statistics.UsageTimeSeries(filterType, targetIds, clientIds, from, to, granularity),
            cancellationToken);
    }

    public Task<IReadOnlyList<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        IEnumerable<string>? clientIds,
        DateTime? from,
        DateTime? to,
        BucketGranularity? granularity,
        CancellationToken cancellationToken)
    {
        return GetAsync<IReadOnlyList<TargetClientUsageBreakdownResponse>>(
            StorageApiRoutes.Statistics.ClientUsageBreakdown(filterType, targetIds, clientIds, from, to, granularity),
            cancellationToken);
    }

    public Task<ClientSummariesResponse> GetClientSummariesAsync(CancellationToken cancellationToken)
    {
        return GetAsync<ClientSummariesResponse>(StorageApiRoutes.Statistics.ClientSummaries, cancellationToken);
    }

    public Task<IReadOnlyList<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        string? clientId,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        return GetAsync<IReadOnlyList<HistoricalUsageResponse>>(
            StorageApiRoutes.Statistics.HistoricalUsage(filterType, targetIds, clientId, from, to, granularity),
            cancellationToken);
    }

    public async Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.Metrics.Prometheus, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public Task<GrafanaMetricsResponse> GetGrafanaMetricsAsync(CancellationToken cancellationToken)
    {
        return GetAsync<GrafanaMetricsResponse>(StorageApiRoutes.Metrics.Grafana, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException($"The storage API returned an empty response for '{path}'.");
    }

    private async Task<JsonElement> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private async Task<SearchResult<T>> SearchAsync<T>(
        string path,
        DocumentQuery query,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(path, query, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SearchResult<T>>(cancellationToken)
            ?? new SearchResult<T>([], 0);
    }
}