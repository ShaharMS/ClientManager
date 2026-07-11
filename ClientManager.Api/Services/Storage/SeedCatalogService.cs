using System.Diagnostics;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Exports and imports catalog and statistics entities for instance-to-instance copying.
/// </summary>
public sealed partial class SeedCatalogService(
    IEntityRepository<Service> serviceRepository,
    IEntityRepository<ResourcePool> resourcePoolRepository,
    IGlobalRateLimitDatabase globalRateLimitDatabase,
    IClientConfigurationDatabase clientConfigurationDatabase,
    IUsageSnapshotDatabase usageSnapshotDatabase,
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

    public async Task<SeedImportSummary> ImportPostAsync(
        SeedOptions seed,
        SeedCollections collections,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionsEmptyAsync(collections, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var summary = new SeedImportSummary();

        if (collections.HasFlag(SeedCollections.Services))
        {
            summary = await ImportCreateAllAsync(seed.Services, serviceRepository.CreateAsync, summary, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ResourcePools))
        {
            summary = await ImportCreateAllAsync(seed.ResourcePools, resourcePoolRepository.CreateAsync, summary, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            summary = await ImportCreateAllAsync(seed.GlobalRateLimits, globalRateLimitDatabase.CreateAsync, summary, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            summary = await ImportCreateAllAsync(seed.ClientConfigurations, clientConfigurationDatabase.CreateAsync, summary, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.UsageSnapshots) && seed.UsageSnapshots.Count > 0)
        {
            await usageSnapshotDatabase.UpsertManyAsync(seed.UsageSnapshots, cancellationToken);
            summary = summary with { Created = summary.Created + seed.UsageSnapshots.Count };
            cache.InvalidateStatistics();
        }

        if (summary.Created > 0)
        {
            cache.InvalidateCatalog();
        }

        return summary with { ElapsedMs = stopwatch.ElapsedMilliseconds };
    }

    public async Task<SeedImportSummary> ImportWithStrategyAsync(
        SeedOptions seed,
        SeedCollections collections,
        SeedImportStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
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

        if (collections.HasFlag(SeedCollections.UsageSnapshots))
        {
            foreach (var snapshot in seed.UsageSnapshots)
            {
                summary = await ImportSnapshotAsync(snapshot, strategy, summary, cancellationToken);
            }

            if (summary.Created > 0 || summary.Updated > 0)
            {
                cache.InvalidateStatistics();
            }
        }

        if (summary.Created > 0 || summary.Updated > 0)
        {
            cache.InvalidateCatalog();
        }

        return summary with { ElapsedMs = stopwatch.ElapsedMilliseconds };
    }

    public async Task EnsureCollectionsEmptyAsync(SeedCollections collections, CancellationToken cancellationToken = default)
    {
        if (collections.HasFlag(SeedCollections.Services))
        {
            await EnsureEmptyAsync("services", serviceRepository.CountAsync, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ResourcePools))
        {
            await EnsureEmptyAsync("resourcePools", resourcePoolRepository.CountAsync, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            await EnsureEmptyAsync("globalRateLimits", globalRateLimitDatabase.CountAsync, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            await EnsureEmptyAsync("clientConfigurations", clientConfigurationDatabase.CountAsync, cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.UsageSnapshots))
        {
            var count = await usageSnapshotDatabase.CountAllAsync(cancellationToken);
            if (count > 0)
            {
                ThrowNotEmpty("usageSnapshots", count);
            }
        }
    }

    public async Task<SeedImportSummary> DeleteAsync(
        SeedCollections collections,
        Stream? progressOutput,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var summary = new SeedImportSummary();
        var progress = new SeedProgressTracker();

        if (collections.HasFlag(SeedCollections.Services))
        {
            summary = await DeleteCatalogAsync(
                serviceRepository.GetAllAsync,
                entity => entity.Id,
                serviceRepository.DeleteAsync,
                summary,
                cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ResourcePools))
        {
            summary = await DeleteCatalogAsync(
                resourcePoolRepository.GetAllAsync,
                entity => entity.Id,
                resourcePoolRepository.DeleteAsync,
                summary,
                cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            summary = await DeleteCatalogAsync(
                globalRateLimitDatabase.GetAllAsync,
                entity => entity.Id,
                globalRateLimitDatabase.DeleteAsync,
                summary,
                cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            summary = await DeleteCatalogAsync(
                clientConfigurationDatabase.GetAllAsync,
                entity => entity.Id,
                clientConfigurationDatabase.DeleteAsync,
                summary,
                cancellationToken);
        }

        if (collections.HasFlag(SeedCollections.UsageSnapshots))
        {
            var deleted = await DeleteUsageSnapshotsAsync(
                progressOutput,
                progress,
                stopwatch,
                cancellationToken);
            summary = summary with { Deleted = summary.Deleted + deleted };
        }

        if (summary.Deleted > 0)
        {
            cache.InvalidateCatalog();
            cache.InvalidateStatistics();
        }

        summary = summary with
        {
            Processed = summary.Deleted,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };

        if (progressOutput is not null)
        {
            await SeedNdjson.WriteLineAsync(progressOutput, SeedNdjson.SerializeSummaryLine(summary), cancellationToken);
        }

        return summary;
    }

    private async Task<SeedImportSummary> DeleteCatalogAsync<TEntity>(
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

    private async Task<int> DeleteUsageSnapshotsAsync(
        Stream? progressOutput,
        SeedProgressTracker progress,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var deleted = 0;

        while (true)
        {
            var page = await usageSnapshotDatabase.GetPageAsync(0, SeedNdjson.BatchSize, cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            foreach (var snapshot in page)
            {
                await usageSnapshotDatabase.DeleteAsync(snapshot.Id, cancellationToken);
                deleted++;
                progress.Processed++;

                if (progressOutput is not null)
                {
                    await progress.MaybeWriteProgressAsync(progressOutput, stopwatch, cancellationToken);
                }
            }
        }

        return deleted;
    }

    private static async Task EnsureEmptyAsync(
        string collectionName,
        Func<DocumentQuery, CancellationToken, Task<long>> countAsync,
        CancellationToken cancellationToken)
    {
        var count = await countAsync(DocumentQuery.All, cancellationToken);
        if (count > 0)
        {
            ThrowNotEmpty(collectionName, count);
        }
    }

    private static void ThrowNotEmpty(string collectionName, long count) =>
        throw new ConflictException(
            $"{collectionName} is not empty ({count} documents). " +
            $"Use DELETE /api/v1/seed?include={collectionName} to wipe, " +
            $"or PUT /api/v1/seed?include={collectionName}&strategy=skip to merge.");

    private static async Task<SeedImportSummary> ImportCreateAllAsync<TEntity>(
        IReadOnlyList<TEntity> incoming,
        Func<TEntity, CancellationToken, Task> create,
        SeedImportSummary summary,
        CancellationToken cancellationToken) where TEntity : class
    {
        foreach (var entity in incoming)
        {
            await create(entity, cancellationToken);
            summary = summary with { Created = summary.Created + 1 };
        }

        return summary;
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
