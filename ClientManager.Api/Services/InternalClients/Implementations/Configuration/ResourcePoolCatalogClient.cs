using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Implementations.Configuration;

internal sealed class ResourcePoolCatalogClient(HttpClient httpClient) : IResourcePoolCatalogClient
{
    public async Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(StorageApiRoutes.ResourcePools.Search, query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResult<ResourcePool>>(cancellationToken)
            ?? new SearchResult<ResourcePool>([], 0);
    }

    public async Task<ResourcePool?> GetByIdAsync(string poolId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.ResourcePools.ById(poolId), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResourcePool>(cancellationToken);
    }

    public Task CreateAsync(ResourcePool pool, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, StorageApiRoutes.ResourcePools.Search.Replace("/search", string.Empty), pool, cancellationToken);

    public Task UpdateAsync(string poolId, ResourcePool pool, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.ResourcePools.ById(poolId), pool, cancellationToken);

    public Task DeleteAsync(string poolId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.ResourcePools.ById(poolId), body: null, cancellationToken);

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