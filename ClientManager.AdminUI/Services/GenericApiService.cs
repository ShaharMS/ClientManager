using System.Net.Http.Json;
using ClientManager.Shared.Models.Search;

namespace ClientManager.AdminUI.Services;

/// <summary>
/// Shared CRUD + short-lived list cache for catalog API resources.
/// </summary>
public class GenericApiService<TEntity> where TEntity : class
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient;
    private readonly string _routePrefix;
    private List<TEntity>? _cachedAll;
    private DateTime _cachedAllAt;

    protected GenericApiService(IHttpClientFactory httpClientFactory, string routePrefix)
    {
        _httpClient = httpClientFactory.CreateClient("ClientManagerApi");
        _routePrefix = routePrefix.TrimEnd('/');
    }

    public async Task<List<TEntity>> GetAllAsync()
    {
        if (_cachedAll is not null && DateTime.UtcNow - _cachedAllAt < CacheTtl)
        {
            return _cachedAll;
        }

        var result = await SearchAsync(new DocumentQuery { Take = 100 });
        _cachedAll = result.Items.ToList();
        _cachedAllAt = DateTime.UtcNow;
        return _cachedAll;
    }

    public async Task<SearchResult<TEntity>> SearchAsync(DocumentQuery? query = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_routePrefix}/search", query ?? DocumentQuery.All);
        await ApiResponseHandler.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<SearchResult<TEntity>>()
            ?? new SearchResult<TEntity>([], 0);
    }

    public Task<TEntity?> GetByIdAsync(string id) =>
        ApiResponseHandler.GetOptionalFromJsonAsync<TEntity>(_httpClient, $"{_routePrefix}/{id}");

    public async Task CreateAsync(TEntity entity)
    {
        var response = await _httpClient.PostAsJsonAsync(_routePrefix, entity);
        await ApiResponseHandler.EnsureSuccessAsync(response);
        InvalidateCache();
    }

    public async Task UpdateAsync(string id, TEntity entity)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_routePrefix}/{id}", entity);
        await ApiResponseHandler.EnsureSuccessAsync(response);
        InvalidateCache();
    }

    public async Task DeleteAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"{_routePrefix}/{id}");
        await ApiResponseHandler.EnsureSuccessAsync(response);
        InvalidateCache();
    }

    protected void InvalidateCache() => _cachedAll = null;
}
