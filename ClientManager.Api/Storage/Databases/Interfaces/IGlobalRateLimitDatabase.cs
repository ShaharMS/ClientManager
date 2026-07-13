using ClientManager.Api.Storage.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Storage.Databases.Interfaces;

/// <summary>
/// Extends <see cref="IEntityRepository{T}"/> for <see cref="GlobalRateLimit"/> documents.
/// </summary>
/// <remarks>
/// <para>
/// Global limits are keyed by service ID. The dedicated lookup keeps the access-check path from
/// scanning the entire catalog when only one service policy is needed.
/// </para>
/// </remarks>
public interface IGlobalRateLimitDatabase : IEntityRepository<GlobalRateLimit>
{
    /// <summary>
    /// Retrieves a global rate limit by service identifier.
    /// </summary>
    /// <param name="serviceId">The service whose global limit should be loaded.</param>
    /// <param name="cancellationToken">Cancels the lookup if the store is unresponsive.</param>
    /// <returns>The global limit if configured; otherwise <c>null</c>.</returns>
    Task<GlobalRateLimit?> GetByServiceIdAsync(string serviceId, CancellationToken cancellationToken = default);
}
