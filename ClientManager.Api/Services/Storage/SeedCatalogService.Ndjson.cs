using System.Diagnostics;
using ClientManager.Api.Utils;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Storage;

public sealed partial class SeedCatalogService
{
    public async Task ExportNdjsonAsync(
        SeedCollections collections,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var progress = new SeedProgressTracker();

        if (collections.HasFlag(SeedCollections.Services))
        {
            foreach (var entity in await serviceRepository.GetAllAsync(cancellationToken))
            {
                await WriteEntityLineAsync(output, SeedNdjson.TypeService, entity, progress, stopwatch, cancellationToken);
            }
        }

        if (collections.HasFlag(SeedCollections.ResourcePools))
        {
            foreach (var entity in await resourcePoolRepository.GetAllAsync(cancellationToken))
            {
                await WriteEntityLineAsync(output, SeedNdjson.TypeResourcePool, entity, progress, stopwatch, cancellationToken);
            }
        }

        if (collections.HasFlag(SeedCollections.GlobalRateLimits))
        {
            foreach (var entity in await globalRateLimitDatabase.GetAllAsync(cancellationToken))
            {
                await WriteEntityLineAsync(output, SeedNdjson.TypeGlobalRateLimit, entity, progress, stopwatch, cancellationToken);
            }
        }

        if (collections.HasFlag(SeedCollections.ClientConfigurations))
        {
            foreach (var entity in await clientConfigurationDatabase.GetAllAsync(cancellationToken))
            {
                await WriteEntityLineAsync(output, SeedNdjson.TypeClientConfiguration, entity, progress, stopwatch, cancellationToken);
            }
        }

        if (collections.HasFlag(SeedCollections.UsageSnapshots))
        {
            var skip = 0;
            while (true)
            {
                var page = await usageSnapshotDatabase.GetPageAsync(skip, SeedNdjson.BatchSize, cancellationToken);
                if (page.Count == 0)
                {
                    break;
                }

                foreach (var entity in page)
                {
                    await WriteEntityLineAsync(output, SeedNdjson.TypeUsageSnapshot, entity, progress, stopwatch, cancellationToken);
                }

                skip += page.Count;
                if (page.Count < SeedNdjson.BatchSize)
                {
                    break;
                }
            }
        }

        await SeedNdjson.WriteLineAsync(output, SeedNdjson.SerializeProgressLine(progress.Processed, stopwatch.ElapsedMilliseconds), cancellationToken);
        await SeedNdjson.WriteLineAsync(
            output,
            SeedNdjson.SerializeSummaryLine(new SeedImportSummary { Processed = progress.Processed, ElapsedMs = stopwatch.ElapsedMilliseconds }),
            cancellationToken);
    }

    public Task<SeedImportSummary> ImportPostNdjsonAsync(
        Stream input,
        SeedCollections collections,
        Stream progressOutput,
        CancellationToken cancellationToken = default) =>
        ImportNdjsonInternalAsync(input, collections, SeedImportStrategy.Skip, requireEmpty: true, progressOutput, cancellationToken);

    public Task<SeedImportSummary> ImportNdjsonAsync(
        Stream input,
        SeedCollections collections,
        SeedImportStrategy strategy,
        Stream progressOutput,
        CancellationToken cancellationToken = default) =>
        ImportNdjsonInternalAsync(input, collections, strategy, requireEmpty: false, progressOutput, cancellationToken);

    private async Task<SeedImportSummary> ImportNdjsonInternalAsync(
        Stream input,
        SeedCollections collections,
        SeedImportStrategy strategy,
        bool requireEmpty,
        Stream progressOutput,
        CancellationToken cancellationToken)
    {
        if (requireEmpty)
        {
            await EnsureCollectionsEmptyAsync(collections, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        var summary = new SeedImportSummary();
        var progress = new SeedProgressTracker();
        var snapshotBatch = new List<UsageSnapshot>(SeedNdjson.BatchSize);

        await foreach (var entity in SeedNdjsonReader.ReadEntitiesAsync(input, cancellationToken))
        {
            progress.Processed++;
            summary = await ImportNdjsonEntityAsync(
                entity,
                collections,
                strategy,
                requireEmpty,
                snapshotBatch,
                summary,
                cancellationToken);

            await progress.MaybeWriteProgressAsync(progressOutput, stopwatch, cancellationToken);
        }

        if (snapshotBatch.Count > 0 && collections.HasFlag(SeedCollections.UsageSnapshots))
        {
            await usageSnapshotDatabase.UpsertManyAsync(snapshotBatch, cancellationToken);
            snapshotBatch.Clear();
        }

        if (summary.Created > 0 || summary.Updated > 0)
        {
            cache.InvalidateCatalog();
        }

        if (summary.Created > 0 || summary.Updated > 0)
        {
            cache.InvalidateStatistics();
        }

        summary = summary with
        {
            Processed = progress.Processed,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };

        await SeedNdjson.WriteLineAsync(progressOutput, SeedNdjson.SerializeSummaryLine(summary), cancellationToken);
        return summary;
    }

    private async Task<SeedImportSummary> ImportNdjsonEntityAsync(
        SeedNdjsonEntity entity,
        SeedCollections collections,
        SeedImportStrategy strategy,
        bool requireEmpty,
        List<UsageSnapshot> snapshotBatch,
        SeedImportSummary summary,
        CancellationToken cancellationToken)
    {
        switch (entity.Type)
        {
            case SeedNdjson.TypeService when collections.HasFlag(SeedCollections.Services):
                return await ImportOneAsync(
                    SeedNdjsonReader.Deserialize<Service>(entity),
                    serviceRepository.GetByIdAsync,
                    serviceRepository.CreateAsync,
                    serviceRepository.UpdateAsync,
                    strategy,
                    requireEmpty,
                    summary,
                    cancellationToken);

            case SeedNdjson.TypeResourcePool when collections.HasFlag(SeedCollections.ResourcePools):
                return await ImportOneAsync(
                    SeedNdjsonReader.Deserialize<ResourcePool>(entity),
                    resourcePoolRepository.GetByIdAsync,
                    resourcePoolRepository.CreateAsync,
                    resourcePoolRepository.UpdateAsync,
                    strategy,
                    requireEmpty,
                    summary,
                    cancellationToken);

            case SeedNdjson.TypeGlobalRateLimit when collections.HasFlag(SeedCollections.GlobalRateLimits):
                return await ImportOneAsync(
                    SeedNdjsonReader.Deserialize<GlobalRateLimit>(entity),
                    globalRateLimitDatabase.GetByIdAsync,
                    globalRateLimitDatabase.CreateAsync,
                    globalRateLimitDatabase.UpdateAsync,
                    strategy,
                    requireEmpty,
                    summary,
                    cancellationToken);

            case SeedNdjson.TypeClientConfiguration when collections.HasFlag(SeedCollections.ClientConfigurations):
                return await ImportOneAsync(
                    SeedNdjsonReader.Deserialize<ClientConfiguration>(entity),
                    clientConfigurationDatabase.GetByIdAsync,
                    clientConfigurationDatabase.CreateAsync,
                    clientConfigurationDatabase.UpdateAsync,
                    strategy,
                    requireEmpty,
                    summary,
                    cancellationToken);

            case SeedNdjson.TypeUsageSnapshot when collections.HasFlag(SeedCollections.UsageSnapshots):
                var snapshot = SeedNdjsonReader.Deserialize<UsageSnapshot>(entity);
                if (requireEmpty)
                {
                    snapshotBatch.Add(snapshot);
                    if (snapshotBatch.Count >= SeedNdjson.BatchSize)
                    {
                        await usageSnapshotDatabase.UpsertManyAsync(snapshotBatch, cancellationToken);
                        snapshotBatch.Clear();
                    }

                    return summary with { Created = summary.Created + 1 };
                }

                return await ImportSnapshotAsync(snapshot, strategy, summary, cancellationToken);

            default:
                return summary;
        }
    }

    private static async Task<SeedImportSummary> ImportOneAsync<TEntity>(
        TEntity entity,
        Func<string, CancellationToken, Task<TEntity?>> getById,
        Func<TEntity, CancellationToken, Task> create,
        Func<TEntity, CancellationToken, Task> update,
        SeedImportStrategy strategy,
        bool requireEmpty,
        SeedImportSummary summary,
        CancellationToken cancellationToken) where TEntity : class
    {
        if (requireEmpty)
        {
            await create(entity, cancellationToken);
            return summary with { Created = summary.Created + 1 };
        }

        var id = GetEntityId(entity);
        var existing = await getById(id, cancellationToken);
        if (existing is null)
        {
            await create(entity, cancellationToken);
            return summary with { Created = summary.Created + 1 };
        }

        if (strategy == SeedImportStrategy.Skip)
        {
            return summary with { Skipped = summary.Skipped + 1 };
        }

        await update(entity, cancellationToken);
        return summary with { Updated = summary.Updated + 1 };
    }

    private async Task<SeedImportSummary> ImportSnapshotAsync(
        UsageSnapshot snapshot,
        SeedImportStrategy strategy,
        SeedImportSummary summary,
        CancellationToken cancellationToken)
    {
        var existing = await usageSnapshotDatabase.GetByIdAsync(snapshot.Id, cancellationToken);
        if (existing is null)
        {
            await usageSnapshotDatabase.UpsertAsync(snapshot, cancellationToken);
            return summary with { Created = summary.Created + 1 };
        }

        if (strategy == SeedImportStrategy.Skip)
        {
            return summary with { Skipped = summary.Skipped + 1 };
        }

        await usageSnapshotDatabase.UpsertAsync(snapshot, cancellationToken);
        return summary with { Updated = summary.Updated + 1 };
    }

    private static async Task WriteEntityLineAsync(
        Stream output,
        string type,
        object entity,
        SeedProgressTracker progress,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        progress.Processed++;
        await SeedNdjson.WriteLineAsync(output, SeedNdjson.SerializeEntityLine(type, entity), cancellationToken);
        await progress.MaybeWriteProgressAsync(output, stopwatch, cancellationToken);
    }
}
