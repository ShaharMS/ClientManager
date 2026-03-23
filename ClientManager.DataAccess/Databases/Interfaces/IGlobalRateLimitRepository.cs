using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.DataAccess.Databases.Interfaces;

/// <summary>
/// Repository for global rate limit entities with target-based query support.
/// </summary>
public interface IGlobalRateLimitRepository : IEntityRepository<GlobalRateLimit>
{
    /// <summary>
    /// Retrieves a global rate limit by its target identifier and target type.
    /// </summary>
    /// <param name="targetId">The identifier of the target (service or resource pool).</param>
    /// <param name="targetType">The type of target the rate limit applies to.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The matching global rate limit if found; otherwise <c>null</c>.</returns>
    Task<GlobalRateLimit?> GetByTargetAsync(string targetId, TargetType targetType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all global rate limits for a given target type.
    /// </summary>
    /// <param name="targetType">The type of target to filter by.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A read-only list of matching global rate limits.</returns>
    Task<IReadOnlyList<GlobalRateLimit>> GetByTargetTypeAsync(TargetType targetType, CancellationToken cancellationToken = default);
}
