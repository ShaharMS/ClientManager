using System.Text.Json;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Shared Search/GetById/Create/Update/Delete implementation for entity-backed catalogs with read-through caching.
/// </summary>
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
            $"{catalogCachePrefix}:search:{JsonSerializer.Serialize(query)}",
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

    public virtual async Task<IReadOnlyList<PatchItemResult<TEntity>>> PatchAsync(
        IReadOnlyList<JsonElement> patches,
        CancellationToken cancellationToken)
    {
        var results = new List<PatchItemResult<TEntity>>(patches.Count);
        var anyUpdated = false;

        foreach (var patch in patches)
        {
            string? id = null;
            try
            {
                if (patch.ValueKind != JsonValueKind.Object
                    || !patch.TryGetProperty("id", out var idProperty)
                    || idProperty.ValueKind != JsonValueKind.String)
                {
                    throw new Models.Exceptions.BadRequestException("Each patch item must include a non-empty \"id\" property.");
                }

                id = idProperty.GetString();
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new Models.Exceptions.BadRequestException("Each patch item must include a non-empty \"id\" property.");
                }

                var existing = await TryGetByIdAsync(id, cancellationToken);
                if (existing is null)
                {
                    throw NotFound(id);
                }

                var merged = EntityPatchMerger.Merge(existing, patch);
                var updated = ApplyId(merged, id);
                await Repository.UpdateAsync(updated, cancellationToken);
                anyUpdated = true;
                results.Add(new PatchItemResult<TEntity>
                {
                    Id = id,
                    Status = PatchItemStatus.Updated,
                    Entity = updated
                });
            }
            catch (Exception exception)
            {
                results.Add(new PatchItemResult<TEntity>
                {
                    Id = id ?? string.Empty,
                    Status = PatchItemStatus.Failed,
                    Error = PatchResultMapper.ToProblem(exception)
                });
            }
        }

        if (anyUpdated)
        {
            Cache.InvalidateCatalog();
        }

        return results;
    }

    private Task<TEntity?> TryGetByIdAsync(string id, CancellationToken cancellationToken) =>
        Cache.GetOrCreateCatalogAsync(
            $"{catalogCachePrefix}:id:{id}",
            token => Repository.GetByIdAsync(id, token),
            cancellationToken);
}
