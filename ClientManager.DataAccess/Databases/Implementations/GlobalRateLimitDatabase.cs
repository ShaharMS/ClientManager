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
/// <param name="store">The document store to delegate operations to.</param>
public class GlobalRateLimitDatabase(IDocumentStore store) : IGlobalRateLimitDatabase
{
    private const string Collection = "GlobalRateLimit";
    private readonly EntityRepository<GlobalRateLimit> _repository =
        new(store, Collection, limit => limit.Id);

    /// <inheritdoc />
    public Task<GlobalRateLimit?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<GlobalRateLimit>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    /// <inheritdoc />
    public Task CreateAsync(GlobalRateLimit entity, CancellationToken cancellationToken = default) =>
        _repository.CreateAsync(entity, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAsync(GlobalRateLimit entity, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(entity, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);

    /// <inheritdoc />
    public Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _repository.SearchAsync(query, cancellationToken);

    /// <inheritdoc />
    public async Task<GlobalRateLimit?> GetByTargetAsync(string targetId, TargetType targetType, CancellationToken cancellationToken = default)
    {
        var query = BuildTargetQuery(targetType, targetId).WithPagination(0, 1);
        var result = await store.SearchAsync<GlobalRateLimit>(Collection, query, cancellationToken);
        return result.Items.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GlobalRateLimit>> GetByTargetTypeAsync(TargetType targetType, CancellationToken cancellationToken = default)
    {
        var result = await store.SearchAsync<GlobalRateLimit>(Collection, BuildTargetQuery(targetType), cancellationToken);
        return [.. result.Items];
    }

    private static DocumentQuery BuildTargetQuery(TargetType targetType, string? targetId = null)
    {
        var query = new DocumentQuery()
            .Where(nameof(GlobalRateLimit.TargetType), FilterOperator.Equals, targetType.ToString());

        if (targetId is not null)
            query.Where(nameof(GlobalRateLimit.TargetId), FilterOperator.Equals, targetId);

        return query;
    }
}
