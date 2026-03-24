using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IClientConfigurationRepository"/>.
/// Delegates storage to <see cref="IDocumentStore"/> and implements sub-document helpers
/// by loading, modifying, and saving the full document.
/// </summary>
public class ClientConfigurationRepository : IClientConfigurationRepository
{
    private readonly IDocumentStore _store;
    private const string Collection = "ClientConfiguration";

    /// <summary>
    /// Initializes a new instance of <see cref="ClientConfigurationRepository"/>.
    /// </summary>
    /// <param name="store">The document store to delegate operations to.</param>
    public ClientConfigurationRepository(IDocumentStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default) =>
        _store.GetAsync<ClientConfiguration>(Collection, clientId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ClientConfiguration>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.GetAllAsync<ClientConfiguration>(Collection, cancellationToken);

    /// <inheritdoc />
    public Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default) =>
        _store.SetAsync(Collection, configuration.Id, configuration, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default) =>
        _store.SetAsync(Collection, configuration.Id, configuration, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string clientId, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(Collection, clientId, cancellationToken);

    /// <inheritdoc />
    public async Task<ServiceAccessSettings?> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        return config?.Services.GetValueOrDefault(serviceId);
    }

    /// <inheritdoc />
    public async Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        if (config is null)
            return;

        var services = new Dictionary<string, ServiceAccessSettings>(config.Services) { [serviceId] = settings };
        await UpdateAsync(config with { Services = services }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        if (config is null)
            return;

        var services = new Dictionary<string, ServiceAccessSettings>(config.Services);
        services.Remove(serviceId);
        await UpdateAsync(config with { Services = services }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ResourcePoolSettings?> GetResourcePoolSettingsAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        if (config is null)
            return null;

        return config.ResourcePools.TryGetValue(resourcePoolId, out var settings) ? settings : null;
    }

    /// <inheritdoc />
    public async Task SetResourcePoolSettingsAsync(string clientId, string resourcePoolId, ResourcePoolSettings settings, CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        if (config is null)
            return;

        var pools = new Dictionary<string, ResourcePoolSettings>(config.ResourcePools) { [resourcePoolId] = settings };
        await UpdateAsync(config with { ResourcePools = pools }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveResourcePoolSettingsAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        if (config is null)
            return;

        var pools = new Dictionary<string, ResourcePoolSettings>(config.ResourcePools);
        pools.Remove(resourcePoolId);
        await UpdateAsync(config with { ResourcePools = pools }, cancellationToken);
    }
}
