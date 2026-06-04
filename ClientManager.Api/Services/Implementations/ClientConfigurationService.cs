using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public client-configuration requests onto the in-process storage configuration catalog,
/// translating an absent client via <see cref="DomainErrors.Client"/> so the controller never
/// has to perform null checks or reshape the persisted document.
/// </summary>
public class ClientConfigurationService : IClientConfigurationService
{
    private readonly IClientConfigurationCatalogService _clientConfigurationCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationService"/>.
    /// </summary>
    /// <param name="clientConfigurationCatalogService">In-process storage client-configuration catalog.</param>
    public ClientConfigurationService(IClientConfigurationCatalogService clientConfigurationCatalogService)
    {
        _clientConfigurationCatalogService = clientConfigurationCatalogService;
    }

    /// <inheritdoc />
    public Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _clientConfigurationCatalogService.SearchAsync(query, cancellationToken);

    /// <inheritdoc />
    public async Task<ClientConfiguration> GetByIdAsync(string clientId, CancellationToken cancellationToken = default) =>
        await _clientConfigurationCatalogService.GetByIdAsync(clientId, cancellationToken)
            ?? throw DomainErrors.Client(clientId);

    /// <inheritdoc />
    public async Task<ClientConfiguration> CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await _clientConfigurationCatalogService.CreateAsync(configuration, cancellationToken);
        return configuration;
    }

    /// <inheritdoc />
    public Task<ClientConfiguration> UpdateAsync(string clientId, ClientConfiguration configuration, CancellationToken cancellationToken = default) =>
        _clientConfigurationCatalogService.UpdateAsync(clientId, configuration, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string clientId, CancellationToken cancellationToken = default) =>
        _clientConfigurationCatalogService.DeleteAsync(clientId, cancellationToken);
}
