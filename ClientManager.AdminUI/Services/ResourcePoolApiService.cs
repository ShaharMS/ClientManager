using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.AdminUI.Services;

public class ResourcePoolApiService
{
    private readonly HttpClient _httpClient;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private List<ResourcePool>? _cachedAll;
    private DateTime _cachedAllAt;

    public ResourcePoolApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<ResourcePool>> GetAllAsync()
    {
        if (_cachedAll is not null && DateTime.UtcNow - _cachedAllAt < CacheTtl)
            return _cachedAll;

        var result = await SearchAsync(new DocumentQuery { Take = 100 });
        _cachedAll = result.Items.ToList();
        _cachedAllAt = DateTime.UtcNow;
        return _cachedAll;
    }

    public async Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery? query = null)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/resource-pools/search", query ?? DocumentQuery.All);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResult<ResourcePool>>()
            ?? new SearchResult<ResourcePool>([], 0);
    }

    public async Task<ResourcePool?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<ResourcePool>($"api/v1/resource-pools/{id}");
    }

    public async Task CreateAsync(ResourcePool pool)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/resource-pools", pool);
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }

    public async Task UpdateAsync(string id, ResourcePool pool)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/resource-pools/{id}", pool);
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/resource-pools/{id}");
        response.EnsureSuccessStatusCode();
        _cachedAll = null;
    }
}
