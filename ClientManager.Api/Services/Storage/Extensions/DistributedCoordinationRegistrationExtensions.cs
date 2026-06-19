using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage.Extensions;

/// <summary>
/// Registers distributed coordination services for background workers and cache invalidation.
/// </summary>
public static class DistributedCoordinationRegistrationExtensions
{
    /// <summary>
    /// Adds leader-lock coordination and local catalog cache invalidation.
    /// </summary>
    public static IServiceCollection AddDistributedCoordination(
        this IServiceCollection services,
        PersistenceOptions persistenceOptions)
    {
        services.AddOptions<BackgroundWorkersOptions>()
            .BindConfiguration(BackgroundWorkersOptions.SectionName);

        if (UsesSharedPersistence(persistenceOptions))
        {
            services.AddSingleton<IDistributedLeaderLock>(serviceProvider => new StorageBackedLeaderLock(
                serviceProvider.GetRequiredKeyedService<IDocumentStore>(StorageRole.RateLimiting),
                serviceProvider.GetRequiredService<IOptions<BackgroundWorkersOptions>>(),
                serviceProvider.GetRequiredService<IAppLogger<StorageBackedLeaderLock>>()));
        }
        else
        {
            services.AddSingleton<IDistributedLeaderLock, LocalDistributedLeaderLock>();
        }

        services.AddSingleton<ICrossPodCacheInvalidator, CatalogCacheInvalidator>();
        return services;
    }

    private static bool UsesSharedPersistence(PersistenceOptions options) =>
        !IsSingleHostOnly(ResolveBinding(options, StorageRole.RateLimiting).Provider);

    private static bool IsSingleHostOnly(PersistenceProvider provider) =>
        provider is PersistenceProvider.JsonFile or PersistenceProvider.Lucene;

    private static StorageRoleBinding ResolveBinding(PersistenceOptions options, StorageRole role)
    {
        if (options.Roles is not null && options.Roles.TryGetValue(role, out var explicitBinding))
        {
            return explicitBinding;
        }

        return new StorageRoleBinding
        {
            Provider = options.DefaultProvider,
            Redis = options.DefaultRedis
        };
    }
}
