using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.Storage.Extensions;

/// <summary>
/// Registers distributed coordination services for background workers and cache invalidation.
/// </summary>
public static class DistributedCoordinationRegistrationExtensions
{
    /// <summary>
    /// Adds leader-lock and optional Redis pub/sub cache invalidation.
    /// </summary>
    public static IServiceCollection AddDistributedCoordination(
        this IServiceCollection services,
        PersistenceOptions persistenceOptions)
    {
        services.AddOptions<BackgroundWorkersOptions>()
            .BindConfiguration(BackgroundWorkersOptions.SectionName);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer)))
        {
            services.AddSingleton<IDistributedLeaderLock, RedisDistributedLeaderLock>();
            services.AddHostedService<RedisCacheInvalidationSubscriber>();
            services.AddSingleton<ICrossPodCacheInvalidator, RedisCacheInvalidationPublisher>();
        }
        else
        {
            services.AddSingleton<IDistributedLeaderLock, LocalDistributedLeaderLock>();
            services.AddSingleton<ICrossPodCacheInvalidator, LocalCacheInvalidationPublisher>();
        }

        return services;
    }

    internal static bool RoleUsesRedis(PersistenceOptions options, StorageRole role) =>
        ResolveBinding(options, role).Provider == PersistenceProvider.Redis;

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
