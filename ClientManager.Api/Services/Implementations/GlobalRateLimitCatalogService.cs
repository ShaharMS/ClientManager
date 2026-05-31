using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public global rate-limit catalog requests onto the storage-facing
/// <see cref="IGlobalRateLimitCatalogClient"/>, reconciling route identifiers on update so the
/// controller never has to reshape the persisted document.
/// </summary>
public class GlobalRateLimitCatalogService : IGlobalRateLimitCatalogService
{
    private readonly IGlobalRateLimitCatalogClient _globalRateLimitCatalogClient;

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalRateLimitCatalogService"/>.
    /// </summary>
    /// <param name="globalRateLimitCatalogClient">Typed client for the storage-facing global rate-limit catalog.</param>
    public GlobalRateLimitCatalogService(IGlobalRateLimitCatalogClient globalRateLimitCatalogClient)
    {
        _globalRateLimitCatalogClient = globalRateLimitCatalogClient;
    }

    /// <inheritdoc />
    public Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _globalRateLimitCatalogClient.SearchAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<GlobalRateLimit> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _globalRateLimitCatalogClient.GetByIdAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<GlobalRateLimit> CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken = default)
    {
        await _globalRateLimitCatalogClient.CreateAsync(limit, cancellationToken);
        return limit;
    }

    /// <inheritdoc />
    public async Task<GlobalRateLimit> UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken = default)
    {
        await _globalRateLimitCatalogClient.UpdateAsync(id, limit, cancellationToken);
        return limit with { Id = id };
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _globalRateLimitCatalogClient.DeleteAsync(id, cancellationToken);
}
