using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Implementations.Configuration;

internal sealed class ServiceCatalogClient(HttpClient httpClient) : IServiceCatalogClient
{
    public async Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(StorageApiRoutes.Services.Search, query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResult<Service>>(cancellationToken)
            ?? new SearchResult<Service>([], 0);
    }

    public async Task<Service?> GetByIdAsync(string serviceId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.Services.ById(serviceId), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Service>(cancellationToken);
    }

    public Task CreateAsync(Service service, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, StorageApiRoutes.Services.Search.Replace("/search", string.Empty), service, cancellationToken);

    public Task UpdateAsync(string serviceId, Service service, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.Services.ById(serviceId), service, cancellationToken);

    public Task DeleteAsync(string serviceId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.Services.ById(serviceId), body: null, cancellationToken);

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