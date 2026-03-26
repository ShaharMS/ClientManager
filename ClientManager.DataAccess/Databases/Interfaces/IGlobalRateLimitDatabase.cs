using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Extends <see cref="IEntityRepository{T}"/> for <see cref="GlobalRateLimit"/> with
/// target-scoped lookups.
///
/// <para>
///     During access-control evaluation, the system needs to find the global rate limit
///     <em>for a specific target</em> (e.g. "the global limit on service X"), not by the
///     rate-limit document's own ID. <see cref="GetByTargetAsync"/> serves exactly that
///     hot-path query.
/// </para>
/// </summary>
public interface IGlobalRateLimitDatabase : IEntityRepository<GlobalRateLimit>
{
    /// <summary>
    /// Retrieves a global rate limit by its target identifier and target type.
    /// </summary>
    /// <param name="targetId">The identifier of the target (service or resource pool).</param>
    /// <param name="targetType">The type of target the rate limit applies to.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The matching global rate limit if found; otherwise <c>null</c>.</returns>
    Task<GlobalRateLimit?> GetByTargetAsync(string targetId, TargetType targetType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all global rate limits for a given target type.
    /// </summary>
    /// <param name="targetType">The type of target to filter by.</param>
    /// <param name="cancellationToken">Cancels the enumeration early if the caller is shutting down.</param>
    /// <returns>A read-only list of matching global rate limits.</returns>
    Task<IReadOnlyList<GlobalRateLimit>> GetByTargetTypeAsync(TargetType targetType, CancellationToken cancellationToken = default);
}
