using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Manages the per-resource-pool settings nested under a client configuration.
/// Resolving a missing client or missing settings surfaces a typed not-found exception so the
/// controller never has to inspect null results.
/// </summary>
public interface IClientResourcePoolSettingsService
{
    /// <summary>
    /// Lists the resource pool settings configured for a client, paginated.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="paging">The requested page and page size.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>A page of keyed resource pool settings.</returns>
    Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>> GetResourcePoolsAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the settings for a specific resource pool under a client.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="poolId">The unique identifier of the resource pool.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The resource pool settings.</returns>
    Task<ResourcePoolSettings> GetResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the settings for a resource pool under a client.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="poolId">The unique identifier of the resource pool being configured.</param>
    /// <param name="settings">The settings to apply.</param>
    /// <param name="cancellationToken">Cancels the update operation.</param>
    /// <returns>The applied resource pool settings.</returns>
    Task<ResourcePoolSettings> SetResourcePoolSettingsAsync(string clientId, string poolId, ResourcePoolSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the settings for a resource pool under a client.
    /// </summary>
    /// <param name="clientId">The unique identifier of the owning client.</param>
    /// <param name="poolId">The unique identifier of the resource pool to clear.</param>
    /// <param name="cancellationToken">Cancels the remove operation.</param>
    Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken = default);
}
