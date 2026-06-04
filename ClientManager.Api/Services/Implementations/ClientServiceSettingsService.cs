using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public client service-settings requests onto the in-process storage configuration catalog,
/// translating a missing client into a <see cref="ClientNotFoundException"/> and absent settings into
/// a <see cref="ServiceSettingsNotFoundException"/> so controllers stay free of null checks.
/// </summary>
public class ClientServiceSettingsService : IClientServiceSettingsService
{
    private readonly IClientConfigurationCatalogService _clientConfigurationCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientServiceSettingsService"/>.
    /// </summary>
    /// <param name="clientConfigurationCatalogService">In-process storage client-configuration catalog.</param>
    public ClientServiceSettingsService(IClientConfigurationCatalogService clientConfigurationCatalogService)
    {
        _clientConfigurationCatalogService = clientConfigurationCatalogService;
    }

    /// <inheritdoc />
    public async Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>> GetServicesAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken = default) =>
        await _clientConfigurationCatalogService.GetServicesAsync(clientId, paging, cancellationToken)
            ?? throw new ClientNotFoundException(clientId);

    /// <inheritdoc />
    public async Task<ServiceAccessSettings> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default)
    {
        var lookup = await _clientConfigurationCatalogService.GetServiceSettingsAsync(clientId, serviceId, cancellationToken);
        if (!lookup.ClientExists)
        {
            throw new ClientNotFoundException(clientId);
        }

        return lookup.Value ?? throw new ServiceSettingsNotFoundException(serviceId, clientId);
    }

    /// <inheritdoc />
    public async Task<ServiceAccessSettings> SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken = default)
    {
        await _clientConfigurationCatalogService.SetServiceSettingsAsync(clientId, serviceId, settings, cancellationToken);
        return settings;
    }

    /// <inheritdoc />
    public Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default) =>
        _clientConfigurationCatalogService.RemoveServiceSettingsAsync(clientId, serviceId, cancellationToken);
}
