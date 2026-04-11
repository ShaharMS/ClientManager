using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.InternalClients;
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
        CreateAsync(StorageApiRoutes.Services.Search.Replace("/search", string.Empty), service, cancellationToken);

    public Task UpdateAsync(string serviceId, Service service, CancellationToken cancellationToken) =>
        SendWithNotFoundAsync(HttpMethod.Put, StorageApiRoutes.Services.ById(serviceId), service, () => new ServiceNotFoundException(serviceId), cancellationToken);

    public Task DeleteAsync(string serviceId, CancellationToken cancellationToken) =>
        SendWithNotFoundAsync(HttpMethod.Delete, StorageApiRoutes.Services.ById(serviceId), body: null, () => new ServiceNotFoundException(serviceId), cancellationToken);

    private async Task CreateAsync(string path, Service service, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path, service);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ServiceAlreadyExistsException(service.Id);
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