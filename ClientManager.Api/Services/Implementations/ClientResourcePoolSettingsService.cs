using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public client resource-pool-settings requests onto the storage-facing
/// <see cref="IClientConfigurationStoreClient"/>, translating absent settings into a
/// <see cref="ResourcePoolSettingsNotFoundException"/> so controllers stay free of null checks.
/// </summary>
public class ClientResourcePoolSettingsService : IClientResourcePoolSettingsService
{
    private readonly IClientConfigurationStoreClient _clientConfigurationStoreClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientResourcePoolSettingsService"/>.
    /// </summary>
    /// <param name="clientConfigurationStoreClient">Typed client for the storage-facing configuration store.</param>
    public ClientResourcePoolSettingsService(IClientConfigurationStoreClient clientConfigurationStoreClient)
    {
        _clientConfigurationStoreClient = clientConfigurationStoreClient;
    }

    /// <inheritdoc />
    public Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>> GetResourcePoolsAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken = default) =>
        _clientConfigurationStoreClient.GetResourcePoolsAsync(clientId, paging, cancellationToken);

    /// <inheritdoc />
    public async Task<ResourcePoolSettings> GetResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken = default) =>
        await _clientConfigurationStoreClient.GetResourcePoolSettingsAsync(clientId, poolId, cancellationToken)
            ?? throw new ResourcePoolSettingsNotFoundException(poolId, clientId);

    /// <inheritdoc />
    public async Task<ResourcePoolSettings> SetResourcePoolSettingsAsync(string clientId, string poolId, ResourcePoolSettings settings, CancellationToken cancellationToken = default)
    {
        await _clientConfigurationStoreClient.SetResourcePoolSettingsAsync(clientId, poolId, settings, cancellationToken);
        return settings;
    }

    /// <inheritdoc />
    public Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken = default) =>
        _clientConfigurationStoreClient.RemoveResourcePoolSettingsAsync(clientId, poolId, cancellationToken);
}
