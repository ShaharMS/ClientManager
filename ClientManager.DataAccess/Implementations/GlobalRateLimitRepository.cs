using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IGlobalRateLimitRepository"/>.
/// Delegates storage to <see cref="IDocumentStore"/> and filters in memory.
/// </summary>
public class GlobalRateLimitRepository : EntityRepository<GlobalRateLimit>, IGlobalRateLimitRepository
{
    private readonly IDocumentStore _store;
    private const string Collection = "GlobalRateLimit";

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalRateLimitRepository"/>.
    /// </summary>
    /// <param name="store">The document store to delegate operations to.</param>
    public GlobalRateLimitRepository(IDocumentStore store)
        : base(store, Collection, g => g.Id)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<GlobalRateLimit?> GetByTargetAsync(string targetId, GlobalRateLimitTarget targetType, CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<GlobalRateLimit>(Collection, cancellationToken);
        return all.FirstOrDefault(g => g.TargetId == targetId && g.TargetType == targetType);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GlobalRateLimit>> GetByTargetTypeAsync(GlobalRateLimitTarget targetType, CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<GlobalRateLimit>(Collection, cancellationToken);
        return all.Where(g => g.TargetType == targetType).ToList();
    }
}
