using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Models.Search;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.DataAccess.Databases.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IClientConfigurationDatabase"/>.
/// Delegates storage to <see cref="IDocumentStore"/> and implements sub-document helpers
/// by loading, modifying, and saving the full document.
/// </summary>
/// <param name="store">The document store to delegate operations to.</param>
public class ClientConfigurationDatabase(IDocumentStore store) : IClientConfigurationDatabase
{
    private const string Collection = "ClientConfiguration";

    /// <inheritdoc />
    public Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default) =>
        store.GetAsync<ClientConfiguration>(Collection, clientId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ClientConfiguration>> GetAllAsync(CancellationToken cancellationToken = default) =>
        store.GetAllAsync<ClientConfiguration>(Collection, cancellationToken);

    /// <inheritdoc />
    public Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        store.SearchAsync<ClientConfiguration>(Collection, query, cancellationToken);

    /// <inheritdoc />
    public Task<long> CountAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        store.CountAsync<ClientConfiguration>(Collection, query, cancellationToken);

    /// <inheritdoc />
    public Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default) =>
        store.SetAsync(Collection, configuration.Id, configuration, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default) =>
        store.SetAsync(Collection, configuration.Id, configuration, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string clientId, CancellationToken cancellationToken = default) =>
        store.DeleteAsync(Collection, clientId, cancellationToken);

    /// <inheritdoc />
    public async Task<ServiceAccessSettings?> GetServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        return config?.Services.GetValueOrDefault(serviceId);
    }

    /// <inheritdoc />
    public Task SetServiceSettingsAsync(string clientId, string serviceId, ServiceAccessSettings settings, CancellationToken cancellationToken = default) =>
        MutateAsync(
            clientId,
            config => config.Services,
            (config, services) => config with { Services = services },
            services => services[serviceId] = settings,
            cancellationToken);

    /// <inheritdoc />
    public Task RemoveServiceSettingsAsync(string clientId, string serviceId, CancellationToken cancellationToken = default) =>
        MutateAsync(
            clientId,
            config => config.Services,
            (config, services) => config with { Services = services },
            services => services.Remove(serviceId),
            cancellationToken);

    /// <inheritdoc />
    public async Task<ResourcePoolSettings?> GetResourcePoolSettingsAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        return config?.ResourcePools.GetValueOrDefault(resourcePoolId);
    }

    /// <inheritdoc />
    public Task SetResourcePoolSettingsAsync(string clientId, string resourcePoolId, ResourcePoolSettings settings, CancellationToken cancellationToken = default) =>
        MutateAsync(
            clientId,
            config => config.ResourcePools,
            (config, pools) => config with { ResourcePools = pools },
            pools => pools[resourcePoolId] = settings,
            cancellationToken);

    /// <inheritdoc />
    public Task RemoveResourcePoolSettingsAsync(string clientId, string resourcePoolId, CancellationToken cancellationToken = default) =>
        MutateAsync(
            clientId,
            config => config.ResourcePools,
            (config, pools) => config with { ResourcePools = pools },
            pools => pools.Remove(resourcePoolId),
            cancellationToken);

    private async Task MutateAsync<TValue>(
        string clientId,
        Func<ClientConfiguration, Dictionary<string, TValue>> select,
        Func<ClientConfiguration, Dictionary<string, TValue>, ClientConfiguration> apply,
        Action<Dictionary<string, TValue>> mutate,
        CancellationToken cancellationToken)
    {
        var config = await GetByIdAsync(clientId, cancellationToken);
        if (config is null)
            return;

        var updated = new Dictionary<string, TValue>(select(config));
        mutate(updated);
        await UpdateAsync(apply(config, updated), cancellationToken);
    }
}
