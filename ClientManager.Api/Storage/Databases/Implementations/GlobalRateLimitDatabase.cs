using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Api.Storage.Repositories.Implementations;
using ClientManager.Api.Storage.Stores.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Storage.Databases.Implementations;

/// <summary>
/// Global rate-limit catalog persistence. Document ID equals service ID.
/// </summary>
public sealed class GlobalRateLimitDatabase(IDocumentStore store) : IGlobalRateLimitDatabase
{
    private readonly EntityRepository<GlobalRateLimit> _repository = new(store, "GlobalRateLimit", limit => limit.Id);

    public Task<GlobalRateLimit?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<GlobalRateLimit?> GetByServiceIdAsync(string serviceId, CancellationToken cancellationToken = default) =>
        GetByIdAsync(serviceId, cancellationToken);

    public Task<IReadOnlyList<GlobalRateLimit>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task CreateAsync(GlobalRateLimit entity, CancellationToken cancellationToken = default) =>
        _repository.CreateAsync(entity, cancellationToken);

    public Task UpdateAsync(GlobalRateLimit entity, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(entity, cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);

    public Task<SearchResult<GlobalRateLimit>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _repository.SearchAsync(query, cancellationToken);

    public Task<long> CountAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        _repository.CountAsync(query, cancellationToken);
}
