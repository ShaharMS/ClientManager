using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Api.Storage.Stores.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Storage.Databases.Implementations;

/// <summary>
/// Platform-agnostic implementation of <see cref="IClientConfigurationDatabase"/>.
/// </summary>
public class ClientConfigurationDatabase(IDocumentStore store) : IClientConfigurationDatabase
{
    private const string Collection = "ClientConfiguration";

    public Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default) =>
        store.GetAsync<ClientConfiguration>(Collection, clientId, cancellationToken);

    public Task<IReadOnlyList<ClientConfiguration>> GetAllAsync(CancellationToken cancellationToken = default) =>
        store.GetAllAsync<ClientConfiguration>(Collection, cancellationToken);

    public Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        store.SearchAsync<ClientConfiguration>(Collection, query, cancellationToken);

    public Task<long> CountAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        store.CountAsync<ClientConfiguration>(Collection, query, cancellationToken);

    public Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default) =>
        store.SetAsync(Collection, configuration.Id, configuration, cancellationToken);

    public Task UpdateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default) =>
        store.SetAsync(Collection, configuration.Id, configuration, cancellationToken);

    public Task DeleteAsync(string clientId, CancellationToken cancellationToken = default) =>
        store.DeleteAsync(Collection, clientId, cancellationToken);
}
