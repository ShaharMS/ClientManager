using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Services.Storage.Models.Exceptions;
using ClientManager.Api.Services.Storage.Interfaces;

namespace ClientManager.Api.Services.Storage.Implementations;

/// <summary>
/// Implements global-rate-limit catalog operations.
/// </summary>
public sealed class GlobalRateLimitCatalogService(
    IGlobalRateLimitDatabase database,
    IStorageReadCache cache)
    : GenericEntityCatalogService<GlobalRateLimit>(database, cache, "global-rate-limits"),
        IGlobalRateLimitCatalogService
{
    private readonly IGlobalRateLimitDatabase _database = database;

    protected override string GetEntityId(GlobalRateLimit entity) => entity.Id;

    protected override GlobalRateLimit ApplyId(GlobalRateLimit entity, string id) => entity with { Id = id };

    protected override Exception NotFound(string id) => StorageDomainErrors.GlobalRateLimitNotFound(id);

    protected override Exception AlreadyExists(string id) =>
        throw new InvalidOperationException("Use target-based conflict detection for global rate limits.");

    public override async Task CreateAsync(GlobalRateLimit limit, CancellationToken cancellationToken)
    {
        if (await _database.GetByTargetAsync(limit.TargetId, limit.TargetType, cancellationToken) is not null)
        {
            throw StorageDomainErrors.GlobalRateLimitAlreadyExists(limit.TargetId, limit.TargetType);
        }

        await Repository.CreateAsync(limit, cancellationToken);
        Cache.InvalidateCatalog();
    }
}
