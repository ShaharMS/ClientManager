using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Standard catalog CRUD surface shared by service, resource-pool, and global-rate-limit catalogs.
/// </summary>
/// <typeparam name="TEntity">The catalog entity type.</typeparam>
public interface ICatalogCrudService<TEntity> where TEntity : class
{
    Task<SearchResult<TEntity>> SearchAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    Task<TEntity> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<TEntity> UpdateAsync(string id, TEntity entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
