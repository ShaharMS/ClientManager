using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Services.Storage.Models.Exceptions;
using ClientManager.Api.Services.Storage.Interfaces;

namespace ClientManager.Api.Services.Storage.Implementations;

/// <summary>
/// Implements resource-pool catalog operations.
/// </summary>
public sealed class ResourcePoolCatalogService(
    IEntityRepository<ResourcePool> repository,
    IStorageReadCache cache)
    : GenericEntityCatalogService<ResourcePool>(repository, cache, "resource-pools"),
        IResourcePoolCatalogService
{
    protected override string GetEntityId(ResourcePool entity) => entity.Id;

    protected override ResourcePool ApplyId(ResourcePool entity, string id) => entity with { Id = id };

    protected override Exception NotFound(string id) => new ResourcePoolNotFoundException(id);

    protected override Exception AlreadyExists(string id) => new ResourcePoolAlreadyExistsException(id);
}
