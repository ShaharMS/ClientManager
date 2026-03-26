using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services;

public class ClientApiService
{
    private readonly HttpClient _httpClient;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private List<ClientConfiguration>? _cachedAll;
    private DateTime _cachedAllAt;

    public ClientApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<ClientConfiguration>> GetAllAsync()
    {
        if (_cachedAll is not null && DateTime.UtcNow - _cachedAllAt < CacheTtl)
            return _cachedAll;

        var response = await _httpClient.GetFromJsonAsync<PagedResponse<ClientConfiguration>>(
            "api/v1/clients?pageSize=100");
        _cachedAll = response?.Items?.ToList() ?? [];
        _cachedAllAt = DateTime.UtcNow;
        return _cachedAll;
    }

    public async Task<ClientConfiguration?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<ClientConfiguration>($"api/v1/clients/{id}");
    }

    public async Task CreateAsync(ClientConfiguration config)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/clients", config);
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }

    public async Task UpdateAsync(string id, ClientConfiguration config)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/clients/{id}", config);
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/clients/{id}");
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }
}
