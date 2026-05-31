using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.InternalClients;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Shared.Contracts.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Implementations.Configuration;

// CR: Class should have some documentation for itself, and should inherit documentation for methods, or provide some alternative one if necessary for a specific method.
internal sealed class ResourcePoolCatalogClient(HttpClient httpClient) : IResourcePoolCatalogClient
{
    public async Task<SearchResult<ResourcePool>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(StorageApiRoutes.ResourcePools.Search, query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResult<ResourcePool>>(cancellationToken)
            ?? new SearchResult<ResourcePool>([], 0);
    }

    public async Task<ResourcePool> GetByIdAsync(string poolId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.ResourcePools.ById(poolId), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ResourcePoolNotFoundException(poolId);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResourcePool>(cancellationToken)
            ?? throw new ResourcePoolNotFoundException(poolId);
    }

    public Task CreateAsync(ResourcePool pool, CancellationToken cancellationToken) =>
        CreateAsync(StorageApiRoutes.ResourcePools.Search.Replace("/search", string.Empty), pool, cancellationToken);

    public Task UpdateAsync(string poolId, ResourcePool pool, CancellationToken cancellationToken) =>
        SendWithNotFoundAsync(HttpMethod.Put, StorageApiRoutes.ResourcePools.ById(poolId), pool, () => new ResourcePoolNotFoundException(poolId), cancellationToken);

    public Task DeleteAsync(string poolId, CancellationToken cancellationToken) =>
        SendWithNotFoundAsync(HttpMethod.Delete, StorageApiRoutes.ResourcePools.ById(poolId), body: null, () => new ResourcePoolNotFoundException(poolId), cancellationToken);

    private async Task CreateAsync(string path, ResourcePool pool, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path, pool);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ResourcePoolAlreadyExistsException(pool.Id);
        }

        throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
    }

    private async Task SendWithNotFoundAsync(
        HttpMethod method,
        string path,
        object? body,
        Func<Exception> createNotFoundException,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // CR: does this really need to be a function? why not just a parameter of type `Exception`?
            throw createNotFoundException();
        }

        throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}