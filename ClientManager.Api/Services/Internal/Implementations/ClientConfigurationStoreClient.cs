using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Api.Utils.StorageApi;
using ClientManager.Shared.Contracts.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Internal.Implementations;

/// <summary>
/// Typed HTTP client over the storage API's client-configuration routes.
/// Translates storage problem responses into domain exceptions so the public controllers
/// see strongly typed failures instead of raw transport errors.
/// </summary>
internal sealed class ClientConfigurationStoreClient(HttpClient httpClient) : IClientConfigurationStoreClient
{
    /// <inheritdoc />
    public async Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostRetryableAsJsonAsync(StorageApiRoutes.ClientConfigurations.Search, query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResult<ClientConfiguration>>(cancellationToken)
            ?? new SearchResult<ClientConfiguration>([], 0);
    }

    /// <inheritdoc />
    public async Task<ClientConfiguration> GetByIdAsync(string clientId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.ClientConfigurations.ById(clientId), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ClientNotFoundException(clientId);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<ClientConfiguration>(cancellationToken)
            ?? throw new ClientNotFoundException(clientId);
    }

    /// <inheritdoc />
    public Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, StorageApiRoutes.ClientConfigurations.Search.Replace("/search", string.Empty), configuration, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.ClientConfigurations.ById(clientId), configuration, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string clientId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.ClientConfigurations.ById(clientId), body: null, cancellationToken);

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<ServiceAccessSettings?> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken) =>
        GetOptionalForClientAsync<ServiceAccessSettings>(
            StorageApiRoutes.ClientConfigurations.ServiceSettings(clientId, serviceId),
            clientId,
            StorageErrorCodes.ServiceSettingsNotFound,
            cancellationToken);

    /// <inheritdoc />
    public Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.ClientConfigurations.ServiceSettings(clientId, serviceId), settings, cancellationToken);

    /// <inheritdoc />
    public Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.ClientConfigurations.ServiceSettings(clientId, serviceId), body: null, cancellationToken);

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<ResourcePoolSettings?> GetResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken) =>
        GetOptionalForClientAsync<ResourcePoolSettings>(
            StorageApiRoutes.ClientConfigurations.ResourcePoolSettings(clientId, poolId),
            clientId,
            StorageErrorCodes.ResourcePoolSettingsNotFound,
            cancellationToken);

    /// <inheritdoc />
    public Task SetResourcePoolSettingsAsync(string clientId, string poolId, ResourcePoolSettings settings, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.ClientConfigurations.ResourcePoolSettings(clientId, poolId), settings, cancellationToken);

    /// <inheritdoc />
    public Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.ClientConfigurations.ResourcePoolSettings(clientId, poolId), body: null, cancellationToken);

    /// <inheritdoc />
    public Task<ClientRateLimit?> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken) =>
        GetOptionalForClientAsync<ClientRateLimit>(
            StorageApiRoutes.ClientConfigurations.GlobalRateLimit(clientId),
            clientId,
            StorageErrorCodes.ClientGlobalRateLimitNotFound,
            cancellationToken);

    /// <inheritdoc />
    public Task SetGlobalRateLimitAsync(string clientId, ClientRateLimit rateLimit, CancellationToken cancellationToken) =>
        SendForClientAsync(HttpMethod.Put, StorageApiRoutes.ClientConfigurations.GlobalRateLimit(clientId), rateLimit, clientId, cancellationToken);

    /// <inheritdoc />
    public Task RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken) =>
        SendForClientAsync(HttpMethod.Delete, StorageApiRoutes.ClientConfigurations.GlobalRateLimit(clientId), body: null, clientId, cancellationToken);

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
