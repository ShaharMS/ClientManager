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
/// Typed HTTP client over the storage API's global rate-limit catalog routes.
/// Maps storage conflict and not-found responses onto domain exceptions for the public controllers.
/// </summary>
internal sealed class GlobalRateLimitCatalogClient(HttpClient httpClient) : IGlobalRateLimitCatalogClient
{
    /// <inheritdoc />
    public async Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostRetryableAsJsonAsync(StorageApiRoutes.GlobalRateLimits.Search, query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResult<GlobalRateLimit>>(cancellationToken)
            ?? new SearchResult<GlobalRateLimit>([], 0);
    }

    /// <inheritdoc />
    public async Task<GlobalRateLimit> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(StorageApiRoutes.GlobalRateLimits.ById(id), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new GlobalRateLimitNotFoundException(id);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<GlobalRateLimit>(cancellationToken)
            ?? throw new GlobalRateLimitNotFoundException(id);
    }

    /// <inheritdoc />
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

        if (!response.IsSuccessStatusCode)
        {
            throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, StorageApiRoutes.GlobalRateLimits.ById(id), limit, new GlobalRateLimitNotFoundException(id), cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, StorageApiRoutes.GlobalRateLimits.ById(id), body: null, new GlobalRateLimitNotFoundException(id), cancellationToken);

    private async Task SendAsync(
        HttpMethod method,
        string path,
        object? body,
        Exception notFoundException,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

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
}
