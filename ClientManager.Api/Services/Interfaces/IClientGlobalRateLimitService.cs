using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the optional client-wide global rate limit nested under a client configuration.
/// Resolving a client that has no configured limit surfaces a typed not-found exception so the
/// controller never has to inspect null results.
/// </summary>
public interface IClientGlobalRateLimitService
{
    /// <summary>
    /// Gets the client-wide global rate limit.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The configured client rate limit.</returns>
    Task<ClientRateLimit> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the client-wide global rate limit.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="rateLimit">The rate limit to apply.</param>
    /// <param name="cancellationToken">Cancels the update operation.</param>
    /// <returns>The applied client rate limit.</returns>
    Task<ClientRateLimit> SetGlobalRateLimitAsync(string clientId, ClientRateLimit rateLimit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the client-wide global rate limit.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="cancellationToken">Cancels the remove operation.</param>
    Task RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default);
}
