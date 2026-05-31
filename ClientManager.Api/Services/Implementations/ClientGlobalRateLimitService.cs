using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Adapts public client global-rate-limit requests onto the storage-facing
/// <see cref="IClientConfigurationStoreClient"/>, translating an absent limit into a
/// <see cref="ClientGlobalRateLimitNotFoundException"/> so controllers stay free of null checks.
/// </summary>
public class ClientGlobalRateLimitService : IClientGlobalRateLimitService
{
    private readonly IClientConfigurationStoreClient _clientConfigurationStoreClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ClientGlobalRateLimitService"/>.
    /// </summary>
    /// <param name="clientConfigurationStoreClient">Typed client for the storage-facing configuration store.</param>
    public ClientGlobalRateLimitService(IClientConfigurationStoreClient clientConfigurationStoreClient)
    {
        _clientConfigurationStoreClient = clientConfigurationStoreClient;
    }

    /// <inheritdoc />
    public async Task<ClientRateLimit> GetGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default) =>
        await _clientConfigurationStoreClient.GetGlobalRateLimitAsync(clientId, cancellationToken)
            ?? throw new ClientGlobalRateLimitNotFoundException(clientId);

    /// <inheritdoc />
    public async Task<ClientRateLimit> SetGlobalRateLimitAsync(string clientId, ClientRateLimit rateLimit, CancellationToken cancellationToken = default)
    {
        await _clientConfigurationStoreClient.SetGlobalRateLimitAsync(clientId, rateLimit, cancellationToken);
        return rateLimit;
    }

    /// <inheritdoc />
    public Task RemoveGlobalRateLimitAsync(string clientId, CancellationToken cancellationToken = default) =>
        _clientConfigurationStoreClient.RemoveGlobalRateLimitAsync(clientId, cancellationToken);
}
