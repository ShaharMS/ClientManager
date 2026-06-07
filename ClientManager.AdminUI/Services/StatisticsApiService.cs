using System.Net.Http.Json;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.AdminUI.Services;

public class StatisticsApiService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("ClientManagerApi");

    public Task<SystemOverviewResponse?> GetOverviewAsync() =>
        ApiResponseHandler.GetFromJsonAsync<SystemOverviewResponse>(_httpClient, "api/v1/statistics/overview");

    public async Task<List<ResourcePoolStatisticsResponse>> GetResourcePoolStatsAsync()
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/v1/statistics/resource-pools/search",
            new DocumentQuery { Take = 100 });
        await ApiResponseHandler.EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<SearchResult<ResourcePoolStatisticsResponse>>();
        return result?.Items.ToList() ?? [];
    }

    public Task<GlobalUsageStatsResponse?> GetGlobalUsageStatsAsync() =>
        ApiResponseHandler.GetFromJsonAsync<GlobalUsageStatsResponse>(_httpClient, "api/v1/statistics/global-usage");

    public async Task<List<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        string filterType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, string? granularity = null)
    {
        var url = StatisticsQueryBuilder.BuildTargetQuery(
            "api/v1/statistics/usage-timeseries", filterType, targetIds, clientIds, from, to, granularity);
        return string.IsNullOrEmpty(url)
            ? []
            : await ApiResponseHandler.GetFromJsonAsync<List<TargetUsageTimeSeriesResponse>>(_httpClient, url) ?? [];
    }

    public async Task<List<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        string filterType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, string? granularity = null)
    {
        var url = StatisticsQueryBuilder.BuildTargetQuery(
            "api/v1/statistics/client-usage-breakdown", filterType, targetIds, clientIds, from, to, granularity);
        return string.IsNullOrEmpty(url)
            ? []
            : await ApiResponseHandler.GetFromJsonAsync<List<TargetClientUsageBreakdownResponse>>(_httpClient, url) ?? [];
    }

    public async Task<List<ClientSummaryRow>> GetClientSummariesAsync()
    {
        var response = await ApiResponseHandler.GetFromJsonAsync<PagedResponse<ClientSummaryRow>>(
            _httpClient,
            "api/v1/statistics/client-summaries?pageSize=100");
        return response?.Items?.ToList() ?? [];
    }

    public async Task<List<HistoricalUsageResponse>> GetHistoricalUsageAsync(
        string filterType, IEnumerable<string> targetIds, string? clientId,
        DateTime from, DateTime to, string granularity)
    {
        var url = StatisticsQueryBuilder.BuildTargetQuery(
            "api/v1/statistics/historical-usage", filterType, targetIds,
            from: from, to: to, granularity: granularity, singleClientId: clientId);
        return string.IsNullOrEmpty(url)
            ? []
            : await ApiResponseHandler.GetFromJsonAsync<List<HistoricalUsageResponse>>(_httpClient, url) ?? [];
    }

    public async Task<List<ClientHistoricalUsageResponse>> GetHistoricalUsageByClientAsync(
        string filterType, IEnumerable<string> targetIds, IEnumerable<string> clientIds,
        DateTime from, DateTime to, string granularity)
    {
        var clientIdList = clientIds.Distinct().ToList();
        if (clientIdList.Count == 0)
        {
            return [];
        }

        var url = StatisticsQueryBuilder.BuildTargetQuery(
            "api/v1/statistics/historical-usage/by-client", filterType, targetIds, clientIdList,
            from, to, granularity);
        return string.IsNullOrEmpty(url)
            ? []
            : await ApiResponseHandler.GetFromJsonAsync<List<ClientHistoricalUsageResponse>>(_httpClient, url) ?? [];
    }
}
