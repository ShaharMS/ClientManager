using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public client-configuration requests onto the storage-facing
/// <see cref="IClientConfigurationStoreClient"/>, reconciling route identifiers on update so the
/// controller never has to reshape the persisted document.
/// </summary>
public class ClientConfigurationService : IClientConfigurationService
{
    private readonly IClientConfigurationStoreClient _clientConfigurationStoreClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationService"/>.
    /// </summary>
    /// <param name="clientConfigurationStoreClient">Typed client for the storage-facing configuration store.</param>
    public ClientConfigurationService(IClientConfigurationStoreClient clientConfigurationStoreClient)
    {
        _clientConfigurationStoreClient = clientConfigurationStoreClient;
    }

    /// <inheritdoc />
    public Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _clientConfigurationStoreClient.SearchAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<ClientConfiguration> GetByIdAsync(string clientId, CancellationToken cancellationToken = default) =>
        _clientConfigurationStoreClient.GetByIdAsync(clientId, cancellationToken);

    /// <inheritdoc />
    public async Task<ClientConfiguration> CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await _clientConfigurationStoreClient.CreateAsync(configuration, cancellationToken);
        return configuration;
    }

    /// <inheritdoc />
    public async Task<ClientConfiguration> UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await _clientConfigurationStoreClient.UpdateAsync(clientId, configuration, cancellationToken);
        return configuration with { Id = clientId };
    }

    /// <inheritdoc />
    public Task DeleteAsync(string clientId, CancellationToken cancellationToken = default) =>
        _clientConfigurationStoreClient.DeleteAsync(clientId, cancellationToken);
}
