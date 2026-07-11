using System.Net.Http.Json;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services;

public class StatisticsApiService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("ClientManagerApi");

    public Task<SystemOverviewResponse?> GetOverviewAsync() =>
        ApiResponseHandler.GetFromJsonAsync<SystemOverviewResponse>(_httpClient, "api/v1/statistics/overview");

    public async Task<TimeseriesSearchResponse?> SearchTimeseriesAsync(TimeseriesSearchRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/statistics/timeseries/search", request);
        await ApiResponseHandler.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TimeseriesSearchResponse>();
    }
}
