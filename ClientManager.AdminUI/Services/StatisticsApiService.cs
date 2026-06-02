using System.Net.Http.Json;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.AdminUI.Services;

public class StatisticsApiService
{
    private readonly HttpClient _httpClient;

    public StatisticsApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<SystemOverviewResponse?> GetOverviewAsync()
    {
        return await ApiResponseHandler.GetFromJsonAsync<SystemOverviewResponse>(_httpClient, "api/v1/statistics/overview");
    }

    public async Task<List<ResourcePoolStatisticsResponse>> GetResourcePoolStatsAsync()
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/v1/statistics/resource-pools/search",
            new DocumentQuery { Take = 100 });
        await ApiResponseHandler.EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<SearchResult<ResourcePoolStatisticsResponse>>();
        return result?.Items.ToList() ?? [];
    }

    public async Task<GlobalUsageStatsResponse?> GetGlobalUsageStatsAsync()
    {
        return await ApiResponseHandler.GetFromJsonAsync<GlobalUsageStatsResponse>(_httpClient, "api/v1/statistics/global-usage");
    }

    public async Task<List<TargetUsageTimeSeriesResponse>> GetUsageTimeSeriesAsync(
        string filterType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, string? granularity = null)
    {
        var targetIdList = targetIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (targetIdList.Count == 0)
        {
            return [];
        }

        var url = $"api/v1/statistics/usage-timeseries?filterType={Uri.EscapeDataString(filterType)}&targetIds={Uri.EscapeDataString(string.Join(",", targetIdList))}";
        if (clientIds?.Any() == true)
        {
            url += $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIds))}";
        }
        if (from is not null)
            url += $"&from={from.Value:O}";
        if (to is not null)
            url += $"&to={to.Value:O}";
        if (granularity is not null)
            url += $"&granularity={Uri.EscapeDataString(granularity)}";

        return await ApiResponseHandler.GetFromJsonAsync<List<TargetUsageTimeSeriesResponse>>(_httpClient, url) ?? [];
    }

    public async Task<List<TargetClientUsageBreakdownResponse>> GetClientUsageBreakdownAsync(
        string filterType, IEnumerable<string> targetIds, IEnumerable<string>? clientIds,
        DateTime? from = null, DateTime? to = null, string? granularity = null)
    {
        var targetIdList = targetIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (targetIdList.Count == 0)
        {
            return [];
        }

        var url = $"api/v1/statistics/client-usage-breakdown?filterType={Uri.EscapeDataString(filterType)}&targetIds={Uri.EscapeDataString(string.Join(",", targetIdList))}";
        if (clientIds?.Any() == true)
        {
            url += $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIds))}";
        }
        if (from is not null)
            url += $"&from={from.Value:O}";
        if (to is not null)
            url += $"&to={to.Value:O}";
        if (granularity is not null)
            url += $"&granularity={Uri.EscapeDataString(granularity)}";

        return await ApiResponseHandler.GetFromJsonAsync<List<TargetClientUsageBreakdownResponse>>(_httpClient, url) ?? [];
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
        var targetIdList = targetIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (targetIdList.Count == 0)
        {
            return [];
        }

        var url = $"api/v1/statistics/historical-usage?filterType={Uri.EscapeDataString(filterType)}"
            + $"&targetIds={Uri.EscapeDataString(string.Join(",", targetIdList))}"
            + $"&from={from:O}&to={to:O}"
            + $"&granularity={Uri.EscapeDataString(granularity)}";
        if (clientId is not null)
        {
            url += $"&clientId={Uri.EscapeDataString(clientId)}";
        }
        return await ApiResponseHandler.GetFromJsonAsync<List<HistoricalUsageResponse>>(_httpClient, url) ?? [];
    }

    public async Task<List<ClientHistoricalUsageResponse>> GetHistoricalUsageByClientAsync(
        string filterType, IEnumerable<string> targetIds, IEnumerable<string> clientIds,
        DateTime from, DateTime to, string granularity)
    {
        var targetIdList = targetIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var clientIdList = clientIds.Distinct().ToList();
        if (targetIdList.Count == 0 || clientIdList.Count == 0)
        {
            return [];
        }

        var url = $"api/v1/statistics/historical-usage/by-client?filterType={Uri.EscapeDataString(filterType)}"
            + $"&targetIds={Uri.EscapeDataString(string.Join(",", targetIdList))}"
            + $"&clientIds={Uri.EscapeDataString(string.Join(",", clientIdList))}"
            + $"&from={from:O}&to={to:O}"
            + $"&granularity={Uri.EscapeDataString(granularity)}";

        return await ApiResponseHandler.GetFromJsonAsync<List<ClientHistoricalUsageResponse>>(_httpClient, url) ?? [];
    }
}
