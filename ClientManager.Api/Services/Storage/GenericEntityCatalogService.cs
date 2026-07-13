using ClientManager.Api.Storage.Repositories.Interfaces;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using ClientManager.Api.Services.Interfaces;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Shared Search/GetById/Create/Update/Delete implementation for entity-backed catalogs with read-through caching.
/// </summary>
/// <remarks>
/// <para>
/// Concrete catalog services inherit this helper to avoid duplicating cache key construction,
/// not-found/conflict translation, and ID assignment. Catalog writes invalidate the read cache
/// after persistence succeeds.
/// </para>
/// </remarks>
public abstract class GenericEntityCatalogService<TEntity>(
    IEntityRepository<TEntity> repository,
    IStorageReadCache cache,
    string catalogCachePrefix)
    where TEntity : class
{
    protected IEntityRepository<TEntity> Repository { get; } = repository;
    protected IStorageReadCache Cache { get; } = cache;

    protected abstract string GetEntityId(TEntity entity);
    protected abstract TEntity ApplyId(TEntity entity, string id);
    protected abstract Exception NotFound(string id);
    protected abstract Exception AlreadyExists(string id);

    public Task<SearchResult<TEntity>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken) =>
        Cache.GetOrCreateCatalogAsync(
            $"{catalogCachePrefix}:search:{System.Text.Json.JsonSerializer.Serialize(query)}",
            token => Repository.SearchAsync(query, token),
            cancellationToken);

    public async Task<TEntity> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await TryGetByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        if (await TryGetByIdAsync(GetEntityId(entity), cancellationToken) is not null)
        {
            throw AlreadyExists(GetEntityId(entity));
        }

        await Repository.CreateAsync(entity, cancellationToken);
        Cache.InvalidateCatalog();
        return entity;
    }

    public virtual async Task<TEntity> UpdateAsync(string id, TEntity entity, CancellationToken cancellationToken)
    {
        if (await TryGetByIdAsync(id, cancellationToken) is null)
        {
            throw NotFound(id);
        }

        var updated = ApplyId(entity, id);
        await Repository.UpdateAsync(updated, cancellationToken);
        Cache.InvalidateCatalog();
        return updated;
    }

    public virtual async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (await TryGetByIdAsync(id, cancellationToken) is null)
        {
            throw NotFound(id);
        }

        await Repository.DeleteAsync(id, cancellationToken);
        Cache.InvalidateCatalog();
    }

    private Task<TEntity?> TryGetByIdAsync(string id, CancellationToken cancellationToken) =>
        Cache.GetOrCreateCatalogAsync(
            $"{catalogCachePrefix}:id:{id}",
            token => Repository.GetByIdAsync(id, token),
            cancellationToken);
}
