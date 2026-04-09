using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Implementations.Configuration;

internal sealed class GlobalRateLimitCatalogClient(HttpClient httpClient) : IGlobalRateLimitCatalogClient
{
    public async Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(StorageApiRoutes.GlobalRateLimits.Search, query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResult<GlobalRateLimit>>(cancellationToken)
            ?? new SearchResult<GlobalRateLimit>([], 0);
    }

    public async Task<GlobalRateLimit?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.GlobalRateLimits.ById(id), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GlobalRateLimit>(cancellationToken);
    }

    public async Task CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, StorageApiRoutes.GlobalRateLimits.Search.Replace("/search", string.Empty))
        {
            Content = JsonContent.Create(limit)
        };

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new GlobalRateLimitAlreadyExistsException(limit.TargetId, limit.TargetType);
        }

        response.EnsureSuccessStatusCode();
    }

    public Task UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.GlobalRateLimits.ById(id), limit, cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.GlobalRateLimits.ById(id), body: null, cancellationToken);

    private async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}