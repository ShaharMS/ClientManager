using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public client service-settings requests onto the storage-facing
/// <see cref="IClientConfigurationStoreClient"/>, translating absent settings into a
/// <see cref="ServiceSettingsNotFoundException"/> so controllers stay free of null checks.
/// </summary>
public class ClientServiceSettingsService : IClientServiceSettingsService
{
    private readonly IClientConfigurationStoreClient _clientConfigurationStoreClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientServiceSettingsService"/>.
    /// </summary>
    /// <param name="clientConfigurationStoreClient">Typed client for the storage-facing configuration store.</param>
    public ClientServiceSettingsService(IClientConfigurationStoreClient clientConfigurationStoreClient)
    {
        _clientConfigurationStoreClient = clientConfigurationStoreClient;
    }

    /// <inheritdoc />
    public Task<PagedResponse<KeyedEntry<ServiceAccessSettings>>> GetServicesAsync(string clientId, PagedRequest paging, CancellationToken cancellationToken = default) =>
        _clientConfigurationStoreClient.GetServicesAsync(clientId, paging, cancellationToken);

    /// <inheritdoc />
    public async Task<ServiceAccessSettings> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default) =>
        await _clientConfigurationStoreClient.GetServiceSettingsAsync(clientId, serviceId, cancellationToken)
            ?? throw new ServiceSettingsNotFoundException(serviceId, clientId);

    /// <inheritdoc />
    public async Task<ServiceAccessSettings> SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken = default)
    {
        await _clientConfigurationStoreClient.SetServiceSettingsAsync(clientId, serviceId, settings, cancellationToken);
        return settings;
    }

    /// <inheritdoc />
    public Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default) =>
        _clientConfigurationStoreClient.RemoveServiceSettingsAsync(clientId, serviceId, cancellationToken);
}
