using ClientManager.Api.Utils;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Exports and imports catalog seed data for copying configuration between instances.
/// </summary>
/// <remarks>
/// <para>
/// Seed operations are intentionally separate from normal CRUD so operators can back up, clone, or
/// restore entire catalogs between environments. All methods honor collection filters and coordinate
/// with <see cref="IStorageReadCache"/> invalidation after writes.
/// </para>
/// <para>
/// Imports are serialized through <see cref="Storage.SeedOperationGate"/> so only one long-running seed
/// job runs at a time across the process.
/// </para>
/// </remarks>
public interface ISeedCatalogService
{
    /// <summary>
    /// Exports the requested catalog collections into a <see cref="SeedOptions"/> JSON bundle.
    /// </summary>
    /// <param name="collections">Collections to include in the export.</param>
    /// <param name="cancellationToken">Cancels the export before it completes.</param>
    /// <returns>The exported seed bundle.</returns>
    Task<SeedOptions> ExportAsync(SeedCollections collections, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports seed data into empty included collections (POST semantics).
    /// </summary>
    /// <param name="seed">The seed bundle to import.</param>
    /// <param name="collections">Collections to import into.</param>
    /// <param name="cancellationToken">Cancels the import before it completes.</param>
    /// <returns>Per-collection import summary.</returns>
    /// <remarks>Returns a conflict when any target collection already contains data.</remarks>
    Task<SeedImportSummary> ImportPostAsync(
        SeedOptions seed,
        SeedCollections collections,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges seed data into existing collections using a per-ID strategy.
    /// </summary>
    /// <param name="seed">The seed bundle to merge.</param>
    /// <param name="collections">Collections to merge into.</param>
    /// <param name="strategy"><c>Skip</c> creates missing IDs only; <c>Replace</c> upserts by ID.</param>
    /// <param name="cancellationToken">Cancels the merge before it completes.</param>
    /// <returns>Per-collection import summary.</returns>
    Task<SeedImportSummary> ImportWithStrategyAsync(
        SeedOptions seed,
        SeedCollections collections,
        SeedImportStrategy strategy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all documents in the requested catalog collections.
    /// </summary>
    /// <param name="collections">Collections to wipe.</param>
    /// <param name="cancellationToken">Cancels the delete before it completes.</param>
    /// <returns>Per-collection delete summary.</returns>
    Task<SeedImportSummary> DeleteAsync(
        SeedCollections collections,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that the requested collections are empty before a POST import.
    /// </summary>
    /// <param name="collections">Collections that must be empty.</param>
    /// <param name="cancellationToken">Cancels the check before it completes.</param>
    Task EnsureCollectionsEmptyAsync(SeedCollections collections, CancellationToken cancellationToken = default);
}
