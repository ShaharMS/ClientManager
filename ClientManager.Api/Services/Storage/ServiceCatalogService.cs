using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Implements service-definition catalog operations.
/// </summary>
public sealed class ServiceCatalogService(
    IEntityRepository<Service> repository,
    IStorageReadCache cache,
    ICrossPodCacheInvalidator cacheInvalidator)
    : GenericEntityCatalogService<Service>(repository, cache, cacheInvalidator, "services"),
        IServiceCatalogService
{
    protected override string GetEntityId(Service entity) => entity.Id;

    protected override Service ApplyId(Service entity, string id) => entity with { Id = id };

    protected override Exception NotFound(string id) => DomainErrors.Service(id);

    protected override Exception AlreadyExists(string id) => DomainErrors.DuplicateService(id);
}
