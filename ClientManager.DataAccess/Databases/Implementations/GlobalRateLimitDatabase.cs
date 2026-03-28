using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Implementations;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IGlobalRateLimitDatabase"/>.
/// Delegates storage to <see cref="IDocumentStore"/> and pushes filtering down
/// to the store via <see cref="DocumentQuery"/>.
/// </summary>
public class GlobalRateLimitDatabase : EntityRepository<GlobalRateLimit>, IGlobalRateLimitDatabase
{
    private readonly IDocumentStore _store;
    private const string Collection = "GlobalRateLimit";

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalRateLimitDatabase"/>.
    /// </summary>
    /// <param name="store">The document store to delegate operations to.</param>
    public GlobalRateLimitDatabase(IDocumentStore store)
        : base(store, Collection, g => g.Id)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<GlobalRateLimit?> GetByTargetAsync(string targetId, TargetType targetType, CancellationToken cancellationToken = default)
    {
        var query = new DocumentQuery()
            .Where(nameof(GlobalRateLimit.TargetId), FilterOperator.Equals, targetId)
            .Where(nameof(GlobalRateLimit.TargetType), FilterOperator.Equals, targetType.ToString())
            .WithPagination(0, 1);

        var result = await _store.SearchAsync<GlobalRateLimit>(Collection, query, cancellationToken);
        return result.Items.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GlobalRateLimit>> GetByTargetTypeAsync(TargetType targetType, CancellationToken cancellationToken = default)
    {
        var query = new DocumentQuery()
            .Where(nameof(GlobalRateLimit.TargetType), FilterOperator.Equals, targetType.ToString());

        var result = await _store.SearchAsync<GlobalRateLimit>(Collection, query, cancellationToken);
        return [.. result.Items];
    }
}
