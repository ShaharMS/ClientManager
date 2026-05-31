using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.InternalClients;
using ClientManager.Shared.Contracts.Storage;
using ClientManager.Shared.Models.Problems;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.InternalClients.Implementations.Configuration;

// CR: Class should have some documentation for itself, and should inherit documentation for methods, or provide some alternative one if necessary for a specific method.
internal sealed class ClientConfigurationStoreClient(HttpClient httpClient) : IClientConfigurationStoreClient
{
    public async Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(StorageApiRoutes.ClientConfigurations.Search, query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResult<ClientConfiguration>>(cancellationToken)
            ?? new SearchResult<ClientConfiguration>([], 0);
    }

    public Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken) =>
        GetOptionalAsync<ClientConfiguration>(StorageApiRoutes.ClientConfigurations.ById(clientId), cancellationToken);

    public Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, StorageApiRoutes.ClientConfigurations.Search.Replace("/search", string.Empty), configuration, cancellationToken);

    public Task UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.ClientConfigurations.ById(clientId), configuration, cancellationToken);

    public Task DeleteAsync(string clientId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.ClientConfigurations.ById(clientId), body: null, cancellationToken);

    public async Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>> GetServicesAsync(
        string clientId,
        PagedRequest paging,
        CancellationToken cancellationToken)
    {
        var clamped = paging.Clamp();
        var response = await httpClient.GetAsync(
            StorageApiRoutes.ClientConfigurations.Services(clientId, clamped.Page, clamped.PageSize),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ClientNotFoundException(clientId);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<KeyedEntry<ServiceAccessSettings>>>(cancellationToken)
            ?? new PagedResponse<KeyedEntry<ServiceAccessSettings>>([], clamped.Page, clamped.PageSize, 0, 0);
    }

    public Task<ServiceAccessSettings?> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken) =>
        GetOptionalForClientAsync<ServiceAccessSettings>(
            StorageApiRoutes.ClientConfigurations.ServiceSettings(clientId, serviceId),
            clientId,
            StorageErrorCodes.ServiceSettingsNotFound,
            cancellationToken);

    public Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.ClientConfigurations.ServiceSettings(clientId, serviceId), settings, cancellationToken);

    public Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.ClientConfigurations.ServiceSettings(clientId, serviceId), body: null, cancellationToken);

    public async Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>> GetResourcePoolsAsync(
        string clientId,
        PagedRequest paging,
        CancellationToken cancellationToken)
    {
        var clamped = paging.Clamp();
        var response = await httpClient.GetAsync(
            StorageApiRoutes.ClientConfigurations.ResourcePools(clientId, clamped.Page, clamped.PageSize),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ClientNotFoundException(clientId);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<KeyedEntry<ResourcePoolSettings>>>(cancellationToken)
            ?? new PagedResponse<KeyedEntry<ResourcePoolSettings>>([], clamped.Page, clamped.PageSize, 0, 0);
    }

    public Task<ResourcePoolSettings?> GetResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken) =>
        GetOptionalForClientAsync<ResourcePoolSettings>(
            StorageApiRoutes.ClientConfigurations.ResourcePoolSettings(clientId, poolId),
            clientId,
            StorageErrorCodes.ResourcePoolSettingsNotFound,
            cancellationToken);

    public Task SetResourcePoolSettingsAsync(string clientId, string poolId, ResourcePoolSettings settings, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.ClientConfigurations.ResourcePoolSettings(clientId, poolId), settings, cancellationToken);

    public Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.ClientConfigurations.ResourcePoolSettings(clientId, poolId), body: null, cancellationToken);

    public Task<ClientRateLimit?> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken) =>
        GetOptionalForClientAsync<ClientRateLimit>(
            StorageApiRoutes.ClientConfigurations.GlobalRateLimit(clientId),
            clientId,
            StorageErrorCodes.ClientGlobalRateLimitNotFound,
            cancellationToken);

    public Task SetGlobalRateLimitAsync(string clientId, ClientRateLimit rateLimit, CancellationToken cancellationToken) =>
        SendForClientAsync(HttpMethod.Put, StorageApiRoutes.ClientConfigurations.GlobalRateLimit(clientId), rateLimit, clientId, cancellationToken);

    public Task RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken) =>
        SendForClientAsync(HttpMethod.Delete, StorageApiRoutes.ClientConfigurations.GlobalRateLimit(clientId), body: null, clientId, cancellationToken);

    private async Task<T?> GetOptionalAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<T?> GetOptionalForClientAsync<T>(
        string path,
        string clientId,
        string missingErrorCode,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
        }

        var problem = await StorageApiResponseReader.ReadProblemAsync(response, cancellationToken);
        return problem.ErrorCode switch
        {
            StorageErrorCodes.ClientNotFound => throw new ClientNotFoundException(clientId),
            var code when code == missingErrorCode => default,
            _ => throw StorageApiResponseReader.CreateUnexpectedException(response, problem)
        };
    }

    private async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
        }
    }

    private async Task SendForClientAsync(HttpMethod method, string path, object? body, string clientId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ClientNotFoundException(clientId);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
        }
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