using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Storage.Databases.Interfaces;

/// <summary>Database for <see cref="ClientConfiguration"/> documents.</summary>
public interface IClientConfigurationDatabase
{
    Task<ClientConfiguration?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SearchResult<ClientConfiguration>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    Task<long> CountAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    Task CreateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);

    Task UpdateAsync(ClientConfiguration configuration, CancellationToken cancellationToken = default);

    Task DeleteAsync(string clientId, CancellationToken cancellationToken = default);
}

