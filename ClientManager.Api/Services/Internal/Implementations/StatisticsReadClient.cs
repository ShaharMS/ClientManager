using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Api.Utils.StorageApi;
using ClientManager.Shared.Contracts.Storage;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Internal.Implementations;

/// <summary>
/// Typed read-only HTTP client over the storage API's statistics and metrics routes.
/// Centralizes empty-payload handling and not-found mapping so statistics controllers receive
/// strongly typed results or domain exceptions instead of raw transport failures.
/// </summary>
internal sealed class StatisticsReadClient(HttpClient httpClient) : IStatisticsReadClient
{
    /// <inheritdoc />
    public async Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken)
    {
        return await GetAsync<SystemOverviewResponse>(
            StorageApiRoutes.Statistics.Overview,
            "The storage API returned an empty overview response.",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<SearchResult<ClientSummaryResponse>> SearchClientSummariesAsync(
        DocumentQuery query,
        CancellationToken cancellationToken)
    {
        return SearchAsync<ClientSummaryResponse>(StorageApiRoutes.Statistics.SearchClientSummaries, query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JsonElement> GetClientDetailsAsync(string clientId, CancellationToken cancellationToken)
    {
        return GetJsonAsync(
            StorageApiRoutes.Statistics.ClientDetails(clientId),
            $"The storage API returned an empty response for '{StorageApiRoutes.Statistics.ClientDetails(clientId)}'.",
            new ClientNotFoundException(clientId),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<SearchResult<ServiceStatisticsResponse>> SearchServiceStatisticsAsync(
        DocumentQuery query,
        CancellationToken cancellationToken)
    {
        return SearchAsync<ServiceStatisticsResponse>(StorageApiRoutes.Statistics.SearchServiceStatistics, query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JsonElement> GetServiceDetailsAsync(string serviceId, CancellationToken cancellationToken)
    {
        return GetJsonAsync(
            StorageApiRoutes.Statistics.ServiceDetails(serviceId),
            $"The storage API returned an empty response for '{StorageApiRoutes.Statistics.ServiceDetails(serviceId)}'.",
            new ServiceNotFoundException(serviceId),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<SearchResult<ResourcePoolStatisticsResponse>> SearchResourcePoolStatisticsAsync(
        DocumentQuery query,
        CancellationToken cancellationToken)
    {
        return SearchAsync<ResourcePoolStatisticsResponse>(
            StorageApiRoutes.Statistics.SearchResourcePoolStatistics,
            query,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<JsonElement> GetResourcePoolDetailsAsync(string resourcePoolId, CancellationToken cancellationToken)
    {
        return GetJsonAsync(
            StorageApiRoutes.Statistics.ResourcePoolDetails(resourcePoolId),
            $"The storage API returned an empty response for '{StorageApiRoutes.Statistics.ResourcePoolDetails(resourcePoolId)}'.",
            new ResourcePoolNotFoundException(resourcePoolId),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(CancellationToken cancellationToken)
    {
        return GetAsync<GlobalUsageStatsResponse>(StorageApiRoutes.Statistics.GlobalUsage, cancellationToken);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<ClientSummariesResponse> GetClientSummariesAsync(CancellationToken cancellationToken)
    {
        return GetAsync<ClientSummariesResponse>(StorageApiRoutes.Statistics.ClientSummaries, cancellationToken);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<IReadOnlyList<ClientHistoricalUsageResponse>> GetHistoricalUsageByClientAsync(
        TargetType filterType,
        IEnumerable<string> targetIds,
        IEnumerable<string> clientIds,
        DateTime from,
        DateTime to,
        BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        return GetAsync<IReadOnlyList<ClientHistoricalUsageResponse>>(
            StorageApiRoutes.Statistics.HistoricalUsageByClient(filterType, targetIds, clientIds, from, to, granularity),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.Metrics.Prometheus, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<GrafanaMetricsResponse> GetGrafanaMetricsAsync(CancellationToken cancellationToken)
    {
        return GetAsync<GrafanaMetricsResponse>(StorageApiRoutes.Metrics.Grafana, cancellationToken);
    }

    private Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        return GetAsync<T>(
            path,
            $"The storage API returned an empty response for '{path}'.",
            cancellationToken);
    }

    private async Task<T> GetAsync<T>(
        string path,
        string missingPayloadErrorMessage,
        CancellationToken cancellationToken,
        Exception? notFoundException = null)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await StorageApiResponseReader.ReadRequiredAsync<T>(response, cancellationToken, missingPayloadErrorMessage);
        }

        if (response.StatusCode == HttpStatusCode.NotFound && notFoundException is not null)
        {
            throw notFoundException;
        }

        throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
    }

    private async Task<JsonElement> GetJsonAsync(
        string path,
        string missingPayloadErrorMessage,
        Exception? notFoundException,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await StorageApiResponseReader.ReadRequiredAsync<JsonElement>(response, cancellationToken, missingPayloadErrorMessage);
        }

        if (response.StatusCode == HttpStatusCode.NotFound && notFoundException is not null)
        {
            throw notFoundException;
        }

        throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
    }

    private async Task<SearchResult<T>> SearchAsync<T>(
        string path,
        DocumentQuery query,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostRetryableAsJsonAsync(path, query, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<SearchResult<T>>(cancellationToken)
            ?? new SearchResult<T>([], 0);
    }
}
