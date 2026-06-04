using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;
using StorageGlobalRateLimitCatalogService = ClientManager.Api.Services.Storage.Interfaces.IGlobalRateLimitCatalogService;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public global rate-limit catalog requests onto the in-process storage global rate-limit
/// catalog, translating an absent limit via <see cref="DomainErrors.GlobalRateLimit"/> and
/// reconciling route identifiers on update so the controller never has to reshape the persisted document.
/// </summary>
public class GlobalRateLimitCatalogService : IGlobalRateLimitCatalogService
{
    private readonly StorageGlobalRateLimitCatalogService _globalRateLimitCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalRateLimitCatalogService"/>.
    /// </summary>
    /// <param name="globalRateLimitCatalogService">In-process storage global rate-limit catalog.</param>
    public GlobalRateLimitCatalogService(StorageGlobalRateLimitCatalogService globalRateLimitCatalogService)
    {
        _globalRateLimitCatalogService = globalRateLimitCatalogService;
    }

    /// <inheritdoc />
    public Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _globalRateLimitCatalogService.SearchAsync(query, cancellationToken);

    /// <inheritdoc />
    public async Task<GlobalRateLimit> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _globalRateLimitCatalogService.GetByIdAsync(id, cancellationToken)
            ?? throw DomainErrors.GlobalRateLimit(id);

    /// <inheritdoc />
    public async Task<GlobalRateLimit> CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken = default)
    {
        await _globalRateLimitCatalogService.CreateAsync(limit, cancellationToken);
        return limit;
    }

    /// <inheritdoc />
    public Task<GlobalRateLimit> UpdateAsync(string id, GlobalRateLimit limit, CancellationToken cancellationToken = default) =>
        _globalRateLimitCatalogService.UpdateAsync(id, limit, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _globalRateLimitCatalogService.DeleteAsync(id, cancellationToken);
}
