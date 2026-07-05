using ClientManager.Api.Utils;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Exports and imports catalog seed data in the <see cref="SeedOptions"/> shape.
/// </summary>
public interface ISeedCatalogService
{
    Task<SeedOptions> ExportAsync(SeedCollections collections, CancellationToken cancellationToken = default);

    Task<SeedImportSummary> ReplaceWholesaleAsync(
        SeedOptions seed,
        SeedCollections collections,
        CancellationToken cancellationToken = default);

    Task<SeedImportSummary> ImportWithStrategyAsync(
        SeedOptions seed,
        SeedCollections collections,
        SeedImportStrategy strategy,
        CancellationToken cancellationToken = default);
}
