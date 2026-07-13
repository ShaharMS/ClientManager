using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils;
using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Api.Storage.Repositories.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Exports and imports catalog entities for instance-to-instance copying.
/// </summary>
public sealed class SeedCatalogService(
    IEntityRepository<Service> serviceRepository,
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

    public async Task<SeedImportSummary> ImportPostAsync(
        SeedOptions seed,
        SeedCollections collections,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionsEmptyAsync(collections, cancellationToken);
        return await ImportCoreAsync(seed, collections, SeedImportStrategy.Replace, cancellationToken);
    }

    public Task<SeedImportSummary> ImportWithStrategyAsync(
        SeedOptions seed,
        SeedCollections collections,
        SeedImportStrategy strategy,
        CancellationToken cancellationToken = default) =>
        ImportCoreAsync(seed, collections, strategy, cancellationToken);

    public async Task<SeedImportSummary> DeleteAsync(SeedCollections collections, CancellationToken cancellationToken = default)
    {
        var summary = new SeedImportSummary();

        if (collections.HasFlag(SeedCollections.Services))
        {
            summary = await DeleteCatalogAsync(serviceRepository.GetAllAsync, e => e.Id, serviceRepository.DeleteAsync, summary, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            summary = await DeleteCatalogAsync(globalRateLimitDatabase.GetAllAsync, e => e.Id, globalRateLimitDatabase.DeleteAsync, summary, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            summary = await DeleteCatalogAsync(clientConfigurationDatabase.GetAllAsync, e => e.Id, clientConfigurationDatabase.DeleteAsync, summary, cancellationToken);
        }

        if (summary.Deleted > 0)
        {
            cache.InvalidateCatalog();
        }

        return summary;
    }

    public async Task EnsureCollectionsEmptyAsync(SeedCollections collections, CancellationToken cancellationToken = default)
    {
        if (collections.HasFlag(SeedCollections.Services))
        {
            await EnsureEmptyAsync("services", serviceRepository.CountAsync, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            await EnsureEmptyAsync("globalRateLimits", globalRateLimitDatabase.CountAsync, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            await EnsureEmptyAsync("clientConfigurations", clientConfigurationDatabase.CountAsync, cancellationToken);
        }
    }

    private async Task<SeedImportSummary> ImportCoreAsync(
        SeedOptions seed,
        SeedCollections collections,
        SeedImportStrategy strategy,
        CancellationToken cancellationToken)
    {
        var summary = new SeedImportSummary();

        if (collections.HasFlag(SeedCollections.Services))
        {
            summary = await ImportCollectionAsync(seed.Services, serviceRepository.GetByIdAsync, serviceRepository.CreateAsync, serviceRepository.UpdateAsync, strategy, summary, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            summary = await ImportCollectionAsync(seed.GlobalRateLimits, globalRateLimitDatabase.GetByIdAsync, globalRateLimitDatabase.CreateAsync, globalRateLimitDatabase.UpdateAsync, strategy, summary, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            summary = await ImportCollectionAsync(seed.ClientConfigurations, clientConfigurationDatabase.GetByIdAsync, clientConfigurationDatabase.CreateAsync, clientConfigurationDatabase.UpdateAsync, strategy, summary, cancellationToken);
        }

        if (summary.Created > 0 || summary.Updated > 0)
        {
            cache.InvalidateCatalog();
        }

        return summary;
    }

    private static async Task<SeedImportSummary> DeleteCatalogAsync<TEntity>(
        Func<CancellationToken, Task<IReadOnlyList<TEntity>>> getAll,
        Func<TEntity, string> getId,
        Func<string, CancellationToken, Task> delete,
        SeedImportSummary summary,
        CancellationToken cancellationToken) where TEntity : class
    {
        var existing = await getAll(cancellationToken);
        foreach (var entity in existing)
        {
            await delete(getId(entity), cancellationToken);
        }

        return summary with { Deleted = summary.Deleted + existing.Count };
    }

    private static async Task EnsureEmptyAsync(
        string collectionName,
        Func<DocumentQuery, CancellationToken, Task<long>> countAsync,
        CancellationToken cancellationToken)
    {
        var count = await countAsync(DocumentQuery.All, cancellationToken);
        if (count > 0)
        {
            throw new ConflictException(
                $"{collectionName} is not empty ({count} documents). " +
                $"Use DELETE /api/v1/seed?include={collectionName} to wipe, " +
                $"or PUT /api/v1/seed?include={collectionName}&strategy=skip to merge.");
        }
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
