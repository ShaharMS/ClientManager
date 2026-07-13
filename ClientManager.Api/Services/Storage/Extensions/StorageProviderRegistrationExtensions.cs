using ClientManager.Api.Storage.Stores.Implementations;
using ClientManager.Api.Storage.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Enums;
using ClientManager.Api.Services.Storage.Instrumentation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;

namespace ClientManager.Api.Services.Storage.Extensions;

/// <summary>
/// Registers configured MongoDB and Redis document-store providers.
/// </summary>
public static class StorageProviderRegistrationExtensions
{
    /// <summary>
    /// Adds keyed document-store registrations for every storage role.
    /// </summary>
    public static IServiceCollection AddStorageProviders(
        this IServiceCollection services,
        PersistenceOptions options,
        IHostEnvironment environment)
    {
        ValidateStorageConfiguration(options);

        var mongoClients = new Dictionary<string, IMongoClient>();
        var redisMultiplexers = new Dictionary<string, IConnectionMultiplexer>();
        var storeCreationLock = new object();
        services.AddSingleton(mongoClients);
        services.AddSingleton(redisMultiplexers);

        foreach (var role in Enum.GetValues<StorageRole>())
        {
            var storageRole = role;
            services.AddKeyedSingleton<IDocumentStore>(storageRole, (serviceProvider, _) =>
            {
                var resolvedOptions = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
                var binding = ResolveBinding(resolvedOptions, storageRole);
                IDocumentStore store;
                lock (storeCreationLock)
                {
                    store = DocumentStoreFactory.CreateDocumentStore(
                        binding,
                        serviceProvider.GetRequiredService<Dictionary<string, IMongoClient>>(),
                        serviceProvider.GetRequiredService<Dictionary<string, IConnectionMultiplexer>>());
                }

                return new InstrumentedDocumentStore(
                    store,
                    storageRole,
                    binding.Provider,
                    serviceProvider.GetRequiredService<StorageMetrics>(),
                    serviceProvider.GetRequiredService<IAppLogger<InstrumentedDocumentStore>>());
            });
        }

        LogStorageConfiguration(options);
        return services;
    }

    private static void ValidateStorageConfiguration(PersistenceOptions options)
    {
        foreach (var role in Enum.GetValues<StorageRole>())
        {
            var binding = ResolveBinding(options, role);
            switch (binding.Provider)
            {
                case PersistenceProvider.MongoDb when string.IsNullOrWhiteSpace(binding.MongoDb?.ConnectionString):
                    throw new InvalidOperationException($"Storage role '{role}' uses MongoDb but no ConnectionString was configured.");
                case PersistenceProvider.Redis when string.IsNullOrWhiteSpace(binding.Redis?.Host):
                    throw new InvalidOperationException($"Storage role '{role}' uses Redis but no Host was configured.");
            }
        }
    }

    private static void LogStorageConfiguration(PersistenceOptions options)
    {
        var logger = NLog.LogManager.GetLogger("StorageConfiguration");
        foreach (var role in Enum.GetValues<StorageRole>())
        {
            var binding = ResolveBinding(options, role);
            logger.Info("Storage role {Role} -> {Provider}", role, binding.Provider);
        }
    }

    private static StorageRoleBinding ResolveBinding(PersistenceOptions options, StorageRole role)
    {
        if (options.Roles is not null && options.Roles.TryGetValue(role, out var explicitBinding))
        {
            if (explicitBinding.Provider == PersistenceProvider.Redis)
            {
                return new StorageRoleBinding
                {
                    Provider = explicitBinding.Provider,
                    Redis = MergeRedisOptions(options.DefaultRedis, explicitBinding.Redis)
                };
            }

            return explicitBinding;
        }

        return new StorageRoleBinding
        {
            Provider = options.DefaultProvider,
            MongoDb = options.DefaultMongoDb,
            Redis = options.DefaultRedis
        };
    }

    private static RedisStoreOptions? MergeRedisOptions(RedisStoreOptions? defaultOptions, RedisStoreOptions? overrideOptions)
    {
        if (overrideOptions is null) return defaultOptions;
        if (defaultOptions is null) return overrideOptions;

        return new RedisStoreOptions
        {
            Host = string.IsNullOrWhiteSpace(overrideOptions.Host) ? defaultOptions.Host : overrideOptions.Host,
            Port = overrideOptions.Port == 0 ? defaultOptions.Port : overrideOptions.Port,
            User = overrideOptions.User ?? defaultOptions.User,
            Password = overrideOptions.Password ?? defaultOptions.Password,
            DatabaseIndex = overrideOptions.DatabaseIndex,
            GlobalKeyPrefix = overrideOptions.GlobalKeyPrefix ?? defaultOptions.GlobalKeyPrefix,
            UseSsl = overrideOptions.UseSsl || defaultOptions.UseSsl,
            UseTls = overrideOptions.UseTls || defaultOptions.UseTls,
            TlsCertificatePath = overrideOptions.TlsCertificatePath ?? defaultOptions.TlsCertificatePath,
            TlsCertificatePassword = overrideOptions.TlsCertificatePassword ?? defaultOptions.TlsCertificatePassword,
            AllowInsecureTls = overrideOptions.AllowInsecureTls || defaultOptions.AllowInsecureTls,
            ConnectTimeoutMilliseconds = overrideOptions.ConnectTimeoutMilliseconds,
            ConnectRetry = overrideOptions.ConnectRetry,
            AbortOnConnectFail = overrideOptions.AbortOnConnectFail,
            SyncTimeoutMilliseconds = overrideOptions.SyncTimeoutMilliseconds
        };
    }
}
