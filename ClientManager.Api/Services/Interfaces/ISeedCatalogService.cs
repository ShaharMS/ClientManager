using ClientManager.Api.Utils;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Exports and imports catalog seed data.
/// </summary>
public interface ISeedCatalogService
{
    Task<SeedOptions> ExportAsync(SeedCollections collections, CancellationToken cancellationToken = default);

    Task<SeedImportSummary> ImportPostAsync(
        SeedOptions seed,
        SeedCollections collections,
        CancellationToken cancellationToken = default);

    Task<SeedImportSummary> ImportWithStrategyAsync(
        SeedOptions seed,
        SeedCollections collections,
        SeedImportStrategy strategy,
        CancellationToken cancellationToken = default);

    Task<SeedImportSummary> DeleteAsync(
        SeedCollections collections,
        CancellationToken cancellationToken = default);

    Task EnsureCollectionsEmptyAsync(SeedCollections collections, CancellationToken cancellationToken = default);
}
