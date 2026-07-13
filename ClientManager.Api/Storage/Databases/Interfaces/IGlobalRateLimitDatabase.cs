using ClientManager.Api.Storage.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Storage.Databases.Interfaces;

/// <summary>
/// Extends <see cref="IEntityRepository{T}"/> for <see cref="GlobalRateLimit"/>.
/// </summary>
public interface IGlobalRateLimitDatabase : IEntityRepository<GlobalRateLimit>
{
    /// <summary>
    /// Retrieves a global rate limit by service identifier.
    /// </summary>
    Task<GlobalRateLimit?> GetByServiceIdAsync(string serviceId, CancellationToken cancellationToken = default);
}
