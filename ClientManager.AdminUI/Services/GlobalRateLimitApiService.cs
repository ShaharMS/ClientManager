using System.Net.Http.Json;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Services;

public class GlobalRateLimitApiService
{
    private readonly HttpClient _httpClient;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private List<GlobalRateLimit>? _cachedAll;
    private DateTime _cachedAllAt;
    private readonly Dictionary<GlobalRateLimitTarget, (List<GlobalRateLimit> Data, DateTime At)> _cachedByTarget = new();

    public GlobalRateLimitApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
    }

    public async Task<List<GlobalRateLimit>> GetAllAsync()
    {
        if (_cachedAll is not null && DateTime.UtcNow - _cachedAllAt < CacheTtl)
            return _cachedAll;

        _cachedAll = await _httpClient.GetFromJsonAsync<List<GlobalRateLimit>>("api/global-rate-limits") ?? [];
        _cachedAllAt = DateTime.UtcNow;
        return _cachedAll;
    }

    public async Task<List<GlobalRateLimit>> GetByTargetTypeAsync(GlobalRateLimitTarget targetType)
    {
        if (_cachedByTarget.TryGetValue(targetType, out var cached) && DateTime.UtcNow - cached.At < CacheTtl)
            return cached.Data;

        var data = await _httpClient.GetFromJsonAsync<List<GlobalRateLimit>>(
            $"api/global-rate-limits?targetType={targetType}") ?? [];
        _cachedByTarget[targetType] = (data, DateTime.UtcNow);
        return data;
    }

    public async Task<GlobalRateLimit?> GetByIdAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<GlobalRateLimit>($"api/global-rate-limits/{id}");
    }

    public async Task CreateAsync(GlobalRateLimit limit)
    {
        var response = await _httpClient.PostAsJsonAsync("api/global-rate-limits", limit);
        response.EnsureSuccessStatusCode();
        InvalidateCache();
    }

    public async Task UpdateAsync(string id, GlobalRateLimit limit)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/global-rate-limits/{id}", limit);
        response.EnsureSuccessStatusCode();
        InvalidateCache();
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/global-rate-limits/{id}");
        response.EnsureSuccessStatusCode();
        InvalidateCache();
    }

    private void InvalidateCache()
    {
        _cachedAll = null;
        _cachedByTarget.Clear();
    }
}
