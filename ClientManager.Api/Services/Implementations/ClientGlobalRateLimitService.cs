using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public client global-rate-limit requests onto the in-process storage configuration catalog,
/// translating a missing client into a <see cref="ClientNotFoundException"/> and an absent limit into a
/// <see cref="ClientGlobalRateLimitNotFoundException"/> so controllers stay free of null checks.
/// </summary>
public class ClientGlobalRateLimitService : IClientGlobalRateLimitService
{
    private readonly IClientConfigurationCatalogService _clientConfigurationCatalogService;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientGlobalRateLimitService"/>.
    /// </summary>
    /// <param name="clientConfigurationCatalogService">In-process storage client-configuration catalog.</param>
    public ClientGlobalRateLimitService(IClientConfigurationCatalogService clientConfigurationCatalogService)
    {
        _clientConfigurationCatalogService = clientConfigurationCatalogService;
    }

    /// <inheritdoc />
    public async Task<ClientRateLimit> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var lookup = await _clientConfigurationCatalogService.GetGlobalRateLimitAsync(clientId, cancellationToken);
        if (!lookup.ClientExists)
        {
            throw new ClientNotFoundException(clientId);
        }

        return lookup.Value ?? throw new ClientGlobalRateLimitNotFoundException(clientId);
    }

    /// <inheritdoc />
    public async Task<ClientRateLimit> SetGlobalRateLimitAsync(string clientId, ClientRateLimit rateLimit, CancellationToken cancellationToken = default)
    {
        var updated = await _clientConfigurationCatalogService.SetGlobalRateLimitAsync(clientId, rateLimit, cancellationToken);
        if (!updated)
        {
            throw new ClientNotFoundException(clientId);
        }

        return rateLimit;
    }

    /// <inheritdoc />
    public async Task RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var removed = await _clientConfigurationCatalogService.RemoveGlobalRateLimitAsync(clientId, cancellationToken);
        if (!removed)
        {
            throw new ClientNotFoundException(clientId);
        }
    }
}
