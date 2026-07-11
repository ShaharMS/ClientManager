using ClientManager.Api.Utils;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Exports and imports catalog and statistics seed data.
/// </summary>
public interface ISeedCatalogService
{
    Task<SeedOptions> ExportAsync(SeedCollections collections, CancellationToken cancellationToken = default);

    Task ExportNdjsonAsync(
        SeedCollections collections,
        Stream output,
        CancellationToken cancellationToken = default);

    Task<SeedImportSummary> ImportPostAsync(
        SeedOptions seed,
        SeedCollections collections,
        CancellationToken cancellationToken = default);

    Task<SeedImportSummary> ImportWithStrategyAsync(
        SeedOptions seed,
        SeedCollections collections,
        SeedImportStrategy strategy,
        CancellationToken cancellationToken = default);

    Task<SeedImportSummary> ImportPostNdjsonAsync(
        Stream input,
        SeedCollections collections,
        Stream progressOutput,
        CancellationToken cancellationToken = default);

    Task<SeedImportSummary> ImportNdjsonAsync(
        Stream input,
        SeedCollections collections,
        SeedImportStrategy strategy,
        Stream progressOutput,
        CancellationToken cancellationToken = default);

    Task<SeedImportSummary> DeleteAsync(
        SeedCollections collections,
        Stream? progressOutput,
        CancellationToken cancellationToken = default);

    Task EnsureCollectionsEmptyAsync(SeedCollections collections, CancellationToken cancellationToken = default);
}
