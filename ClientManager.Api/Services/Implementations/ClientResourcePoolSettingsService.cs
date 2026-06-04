using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage;
using ClientManager.Api.Services.Storage.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public client resource-pool-settings requests onto the in-process storage configuration
/// catalog, translating missing clients or settings via <see cref="DomainErrors"/> so controllers stay free of null checks.
/// </summary>
public class ClientResourcePoolSettingsService : IClientResourcePoolSettingsService
{
    private readonly IClientConfigurationCatalogService _clientConfigurationCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientResourcePoolSettingsService"/>.
    /// </summary>
    /// <param name="clientConfigurationCatalogService">In-process storage client-configuration catalog.</param>
    public ClientResourcePoolSettingsService(IClientConfigurationCatalogService clientConfigurationCatalogService)
    {
        _clientConfigurationCatalogService = clientConfigurationCatalogService;
    }

    /// <inheritdoc />
    public async Task<PagedResponse<KeyedEntry<ResourcePoolSettings>>> GetResourcePoolsAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken = default) =>
        await _clientConfigurationCatalogService.GetResourcePoolsAsync(clientId, paging, cancellationToken)
            ?? throw DomainErrors.Client(clientId);

    /// <inheritdoc />
    public async Task<ResourcePoolSettings> GetResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken = default)
    {
        var lookup = await _clientConfigurationCatalogService.GetResourcePoolSettingsAsync(clientId, poolId, cancellationToken);
        return lookup.RequireClientValue(
            clientId,
            DomainErrors.Client,
            () => DomainErrors.ResourcePoolSettings(poolId, clientId));
    }

    /// <inheritdoc />
    public async Task<ResourcePoolSettings> SetResourcePoolSettingsAsync(string clientId, string poolId, ResourcePoolSettings settings, CancellationToken cancellationToken = default)
    {
        await _clientConfigurationCatalogService.SetResourcePoolSettingsAsync(clientId, poolId, settings, cancellationToken);
        return settings;
    }

    /// <inheritdoc />
    public Task RemoveResourcePoolSettingsAsync(string clientId, string poolId, CancellationToken cancellationToken = default) =>
        _clientConfigurationCatalogService.RemoveResourcePoolSettingsAsync(clientId, poolId, cancellationToken);
}
