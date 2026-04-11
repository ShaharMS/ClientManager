using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.AdminUI.Services;

public class ServiceApiService
{
    private readonly HttpClient _httpClient;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private List<Service>? _cachedAll;
    private DateTime _cachedAllAt;

    public ServiceApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<Service>> GetAllAsync()
    {
        if (_cachedAll is not null && DateTime.UtcNow - _cachedAllAt < CacheTtl)
            return _cachedAll;

        var result = await SearchAsync(new DocumentQuery { Take = 100 });
        _cachedAll = result.Items.ToList();
        _cachedAllAt = DateTime.UtcNow;
        return _cachedAll;
    }

    public async Task<SearchResult<Service>> SearchAsync(DocumentQuery? query = null)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/services/search", query ?? DocumentQuery.All);
        await ApiResponseHandler.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<SearchResult<Service>>()
            ?? new SearchResult<Service>([], 0);
    }

    public async Task<Service?> GetByIdAsync(string id)
    {
        return await ApiResponseHandler.GetOptionalFromJsonAsync<Service>(_httpClient, $"api/v1/services/{id}");
    }

    public async Task CreateAsync(Service service)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/services", service);
        await ApiResponseHandler.EnsureSuccessAsync(response);
        _cachedAll = null;
    }

    public async Task UpdateAsync(string id, Service service)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/services/{id}", service);
        await ApiResponseHandler.EnsureSuccessAsync(response);
        _cachedAll = null;
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/services/{id}");
        await ApiResponseHandler.EnsureSuccessAsync(response);
        _cachedAll = null;
    }
}
