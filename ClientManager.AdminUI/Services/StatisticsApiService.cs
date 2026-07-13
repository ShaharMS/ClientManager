namespace ClientManager.AdminUI.Services;

public record DashboardOverview(int TotalClients, int TotalServices, double RequestsPerMinute);

public class StatisticsApiService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("ClientManagerApi");

    public Task<DashboardOverview?> GetOverviewAsync() =>
        ApiResponseHandler.GetFromJsonAsync<DashboardOverview>(_httpClient, "api/v1/statistics/overview");
}
