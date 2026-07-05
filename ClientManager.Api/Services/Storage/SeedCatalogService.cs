using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Exports and imports catalog entities for instance-to-instance permission copying.
/// </summary>
public sealed class SeedCatalogService(
    IEntityRepository<Service> serviceRepository,
    IEntityRepository<ResourcePool> resourcePoolRepository,
    IGlobalRateLimitDatabase globalRateLimitDatabase,
    IClientConfigurationDatabase clientConfigurationDatabase,
    IStorageReadCache cache) : ISeedCatalogService
{
    public async Task<SeedOptions> ExportAsync(SeedCollections collections, CancellationToken cancellationToken = default)
    {
        var seed = new SeedOptions();

        if (collections.HasFlag(SeedCollections.Services))
        {
            seed.Services = (await serviceRepository.GetAllAsync(cancellationToken)).ToList();
        }

        if (collections.HasFlag(SeedCollections.ResourcePools))
        {
            seed.ResourcePools = (await resourcePoolRepository.GetAllAsync(cancellationToken)).ToList();
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            seed.GlobalRateLimits = (await globalRateLimitDatabase.GetAllAsync(cancellationToken)).ToList();
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            seed.ClientConfigurations = (await clientConfigurationDatabase.GetAllAsync(cancellationToken)).ToList();
        }

        return seed;
    }

    public async Task<SeedImportSummary> ReplaceWholesaleAsync(
        SeedOptions seed,
        SeedCollections collections,
        CancellationToken cancellationToken = default)
    {
        var summary = new SeedImportSummary();

        if (collections.HasFlag(SeedCollections.Services))
        {
            var (deleted, created) = await ReplaceCollectionAsync(
                seed.Services,
                serviceRepository.GetAllAsync,
                serviceRepository.DeleteAsync,
                serviceRepository.CreateAsync,
                entity => entity.Id,
                cancellationToken);
            summary = summary with { Deleted = summary.Deleted + deleted, Created = summary.Created + created };
        }

        if (collections.HasFlag(SeedCollections.ResourcePools))
        {
            var (deleted, created) = await ReplaceCollectionAsync(
                seed.ResourcePools,
                resourcePoolRepository.GetAllAsync,
                resourcePoolRepository.DeleteAsync,
                resourcePoolRepository.CreateAsync,
                entity => entity.Id,
                cancellationToken);
            summary = summary with { Deleted = summary.Deleted + deleted, Created = summary.Created + created };
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            var (deleted, created) = await ReplaceCollectionAsync(
                seed.GlobalRateLimits,
                globalRateLimitDatabase.GetAllAsync,
                globalRateLimitDatabase.DeleteAsync,
                globalRateLimitDatabase.CreateAsync,
                entity => entity.Id,
                cancellationToken);
            summary = summary with { Deleted = summary.Deleted + deleted, Created = summary.Created + created };
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            var (deleted, created) = await ReplaceCollectionAsync(
                seed.ClientConfigurations,
                clientConfigurationDatabase.GetAllAsync,
                clientConfigurationDatabase.DeleteAsync,
                clientConfigurationDatabase.CreateAsync,
                entity => entity.Id,
                cancellationToken);
            summary = summary with { Deleted = summary.Deleted + deleted, Created = summary.Created + created };
        }

        cache.InvalidateCatalog();
        return summary;
    }

    public async Task<SeedImportSummary> ImportWithStrategyAsync(
        SeedOptions seed,
        SeedCollections collections,
        SeedImportStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var summary = new SeedImportSummary();

        if (collections.HasFlag(SeedCollections.Services))
        {
            summary = await ImportCollectionAsync(
                seed.Services,
                serviceRepository.GetByIdAsync,
                serviceRepository.CreateAsync,
                serviceRepository.UpdateAsync,
                strategy,
                summary,
                cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ResourcePools))
        {
            summary = await ImportCollectionAsync(
                seed.ResourcePools,
                resourcePoolRepository.GetByIdAsync,
                resourcePoolRepository.CreateAsync,
                resourcePoolRepository.UpdateAsync,
                strategy,
                summary,
                cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            summary = await ImportCollectionAsync(
                seed.GlobalRateLimits,
                globalRateLimitDatabase.GetByIdAsync,
                globalRateLimitDatabase.CreateAsync,
                globalRateLimitDatabase.UpdateAsync,
                strategy,
                summary,
                cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            summary = await ImportCollectionAsync(
                seed.ClientConfigurations,
                clientConfigurationDatabase.GetByIdAsync,
                clientConfigurationDatabase.CreateAsync,
                clientConfigurationDatabase.UpdateAsync,
                strategy,
                summary,
                cancellationToken);
        }

        if (summary.Created > 0 || summary.Updated > 0)
        {
            cache.InvalidateCatalog();
        }

        return summary;
    }

    private static async Task<(int Deleted, int Created)> ReplaceCollectionAsync<TEntity>(
        IReadOnlyList<TEntity> incoming,
        Func<CancellationToken, Task<IReadOnlyList<TEntity>>> getAll,
        Func<string, CancellationToken, Task> delete,
        Func<TEntity, CancellationToken, Task> create,
        Func<TEntity, string> getId,
        CancellationToken cancellationToken) where TEntity : class
    {
        var existing = await getAll(cancellationToken);
        foreach (var entity in existing)
        {
            await delete(getId(entity), cancellationToken);
        }

        foreach (var entity in incoming)
        {
            await create(entity, cancellationToken);
        }

        return (existing.Count, incoming.Count);
    }

    private static async Task<SeedImportSummary> ImportCollectionAsync<TEntity>(
        IReadOnlyList<TEntity> incoming,
        Func<string, CancellationToken, Task<TEntity?>> getById,
        Func<TEntity, CancellationToken, Task> create,
        Func<TEntity, CancellationToken, Task> update,
        SeedImportStrategy strategy,
        SeedImportSummary summary,
        CancellationToken cancellationToken) where TEntity : class
    {
        foreach (var entity in incoming)
        {
            var id = GetEntityId(entity);
            var existing = await getById(id, cancellationToken);
            if (existing is null)
            {
                await create(entity, cancellationToken);
                summary = summary with { Created = summary.Created + 1 };
                continue;
            }

            if (strategy == SeedImportStrategy.Skip)
            {
                summary = summary with { Skipped = summary.Skipped + 1 };
                continue;
            }

            await update(entity, cancellationToken);
            summary = summary with { Updated = summary.Updated + 1 };
        }

        return summary;
    }

    private static string GetEntityId<TEntity>(TEntity entity) where TEntity : class
    {
        var idProperty = typeof(TEntity).GetProperty("Id")
            ?? throw new InvalidOperationException($"Seed entity {typeof(TEntity).Name} must expose an Id property.");

        return (string?)idProperty.GetValue(entity)
            ?? throw new InvalidOperationException($"Seed entity {typeof(TEntity).Name} Id must not be null.");
    }
}
