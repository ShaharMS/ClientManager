using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Api.Utils.StorageApi;
using ClientManager.Shared.Contracts.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Internal.Implementations;

/// <summary>
/// Typed HTTP client over the storage API's service-catalog routes.
/// Maps storage conflict and not-found responses onto domain exceptions for the public controllers.
/// </summary>
internal sealed class ServiceCatalogClient(HttpClient httpClient) : IServiceCatalogClient
{
    /// <inheritdoc />
    public async Task<SearchResult<Service>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostRetryableAsJsonAsync(StorageApiRoutes.Services.Search, query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResult<Service>>(cancellationToken)
            ?? new SearchResult<Service>([], 0);
    }

    /// <inheritdoc />
    public async Task<Service> GetByIdAsync(string serviceId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.Services.ById(serviceId), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ServiceNotFoundException(serviceId);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Service>(cancellationToken)
            ?? throw new ServiceNotFoundException(serviceId);
    }

    /// <inheritdoc />
    public Task CreateAsync(Service service, CancellationToken cancellationToken) =>
        CreateAsync(StorageApiRoutes.Services.Search.Replace("/search", string.Empty), service, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAsync(string serviceId, Service service, CancellationToken cancellationToken) =>
        SendWithNotFoundAsync(HttpMethod.Put, StorageApiRoutes.Services.ById(serviceId), service, new ServiceNotFoundException(serviceId), cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string serviceId, CancellationToken cancellationToken) =>
        SendWithNotFoundAsync(HttpMethod.Delete, StorageApiRoutes.Services.ById(serviceId), body: null, new ServiceNotFoundException(serviceId), cancellationToken);

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
        Exception notFoundException,
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
            throw notFoundException;
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
