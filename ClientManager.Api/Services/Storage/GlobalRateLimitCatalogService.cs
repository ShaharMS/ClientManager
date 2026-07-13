using ClientManager.Api.Storage.Repositories.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Implements global-rate-limit catalog operations.
/// </summary>
public sealed class GlobalRateLimitCatalogService(
    IEntityRepository<GlobalRateLimit> repository,
    IStorageReadCache cache)
    : GenericEntityCatalogService<GlobalRateLimit>(repository, cache, "global-rate-limits"),
        IGlobalRateLimitCatalogService
{
    protected override string GetEntityId(GlobalRateLimit entity) => entity.Id;

    protected override GlobalRateLimit ApplyId(GlobalRateLimit entity, string id) => entity with { Id = id };

    protected override Exception NotFound(string id) => DomainErrors.GlobalRateLimit(id);

    protected override Exception AlreadyExists(string id) => DomainErrors.DuplicateGlobalRateLimit(id);

    public override async Task<GlobalRateLimit> CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        if (await Repository.GetByIdAsync(limit.Id, cancellationToken) is not null)
        {
            throw DomainErrors.DuplicateGlobalRateLimit(limit.Id);
        }

        return await base.CreateAsync(limit, cancellationToken);
    }
}
