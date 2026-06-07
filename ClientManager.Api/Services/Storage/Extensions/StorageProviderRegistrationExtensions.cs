using ClientManager.DataAccess.Stores.Implementations;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Enums;
using ClientManager.Api.Services.Storage.Instrumentation;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using StackExchange.Redis;

namespace ClientManager.Api.Services.Storage.Extensions;

/// <summary>
/// Registers configured document-store providers for the in-process persistence layer.
/// </summary>
public static class StorageProviderRegistrationExtensions
{
    /// <summary>
    /// Adds keyed document-store registrations for every storage role.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The bound persistence options.</param>
    /// <param name="environment">The hosting environment.</param>
    public static IServiceCollection AddStorageProviders(
        this IServiceCollection services,
        PersistenceOptions options,
        IHostEnvironment environment)
    {
        ValidateStorageConfiguration(options);

        var mongoClients = new Dictionary<string, IMongoClient>();
        var redisMultiplexers = new Dictionary<string, IConnectionMultiplexer>();
        var jsonFileStores = new Dictionary<string, JsonFileDocumentStore>(GetPathComparer());
        var luceneStores = new Dictionary<string, LuceneDocumentStore>(GetPathComparer());

        foreach (var role in Enum.GetValues<StorageRole>())
        {
            var binding = ResolveBinding(options, role);
            var store = DocumentStoreFactory.CreateDocumentStore(
                binding,
                jsonFileStores,
                luceneStores,
                mongoClients,
                redisMultiplexers);
            var provider = binding.Provider;
            services.AddKeyedSingleton<IDocumentStore>(role, (serviceProvider, _) =>
                new InstrumentedDocumentStore(
                    store,
                    role,
                    provider,
                    serviceProvider.GetRequiredService<StorageMetrics>(),
                    serviceProvider.GetRequiredService<IAppLogger<InstrumentedDocumentStore>>()));
        }

        LogStorageConfiguration(options, environment);
        return services;
    }

    private static void ValidateStorageConfiguration(PersistenceOptions options)
    {
        if (!Enum.IsDefined(options.DefaultProvider))
        {
            throw new InvalidOperationException(
                $"Persistence:DefaultProvider has invalid value '{options.DefaultProvider}'.");
        }

        foreach (var role in Enum.GetValues<StorageRole>())
        {
            var binding = ResolveBinding(options, role);
            switch (binding.Provider)
            {
                case PersistenceProvider.MongoDb when string.IsNullOrWhiteSpace(binding.MongoDb?.ConnectionString):
                    throw new InvalidOperationException(
                        $"Storage role '{role}' uses MongoDb but no ConnectionString was configured.");

                case PersistenceProvider.Redis when string.IsNullOrWhiteSpace(binding.Redis?.Host):
                    throw new InvalidOperationException(
                        $"Storage role '{role}' uses Redis but no Host was configured.");

                case PersistenceProvider.Redis when binding.Redis?.Host.Contains(':') == true:
                    throw new InvalidOperationException(
                        $"Storage role '{role}' uses Redis but Host '{binding.Redis.Host}' includes a port. Configure only the hostname in Host and put the TCP port in Port.");

                case PersistenceProvider.Redis when binding.Redis?.Port is < 1 or > 65535:
                    throw new InvalidOperationException(
                        $"Storage role '{role}' uses Redis but Port '{binding.Redis?.Port}' is outside 1-65535.");

                case PersistenceProvider.JsonFile when string.IsNullOrWhiteSpace((binding.JsonFile ?? new JsonFileStoreOptions()).DataDirectory):
                    throw new InvalidOperationException(
                        $"Storage role '{role}' uses JsonFile but DataDirectory is empty.");

                case PersistenceProvider.Lucene when string.IsNullOrWhiteSpace((binding.Lucene ?? new LuceneStoreOptions()).IndexDirectory):
                    throw new InvalidOperationException(
                        $"Storage role '{role}' uses Lucene but IndexDirectory is empty.");
            }
        }
    }

    private static void LogStorageConfiguration(PersistenceOptions options, IHostEnvironment environment)
    {
        var logger = NLog.LogManager.GetLogger("StorageConfiguration");

        foreach (var role in Enum.GetValues<StorageRole>())
        {
            var binding = ResolveBinding(options, role);
            var detail = binding.Provider switch
            {
                PersistenceProvider.JsonFile => binding.JsonFile?.DataDirectory ?? "./data",
                PersistenceProvider.MongoDb => MaskConnectionString(binding.MongoDb?.ConnectionString),
                PersistenceProvider.Redis => DescribeRedisEndpoint(binding.Redis),
                PersistenceProvider.Lucene => binding.Lucene?.IndexDirectory ?? "./lucene-index",
                _ => "unknown"
            };

            logger.Info("Storage role {Role} -> {Provider} ({Detail})", role, binding.Provider, detail);

            if (!environment.IsDevelopment()
                && (binding.Provider == PersistenceProvider.JsonFile || binding.Provider == PersistenceProvider.Lucene))
            {
                logger.Warn(
                    "Storage role {Role} is using {Provider}. This backend is intended for local or single-host use and is not safe for multi-instance production deployment.",
                    role,
                    binding.Provider);
            }
        }
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "(not configured)";
        }

        try
        {
            var uri = new Uri(connectionString);
            return $"{uri.Host}:{uri.Port}";
        }
        catch
        {
            var parts = connectionString.Split(',');
            return parts.Length > 0 ? parts[0] : "(masked)";
        }
    }

    private static string DescribeRedisEndpoint(RedisStoreOptions? options)
    {
        if (options is null || string.IsNullOrWhiteSpace(options.Host))
        {
            return "(not configured)";
        }

        var useSsl = options.UseSsl || options.UseTls;
        return $"{options.Host}:{options.Port} (ssl: {useSsl})";
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
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
            Redis = options.DefaultRedis,
            JsonFile = options.DefaultJsonFile,
            Lucene = options.DefaultLucene
        };
    }

    private static RedisStoreOptions? MergeRedisOptions(
        RedisStoreOptions? defaultOptions,
        RedisStoreOptions? overrideOptions)
    {
        if (overrideOptions is null)
        {
            return defaultOptions;
        }

        if (defaultOptions is null)
        {
            return overrideOptions;
        }

        return new RedisStoreOptions
        {
            Host = string.IsNullOrWhiteSpace(overrideOptions.Host) ? defaultOptions.Host : overrideOptions.Host,
            Port = overrideOptions.Port == 0 ? defaultOptions.Port : overrideOptions.Port,
            User = overrideOptions.User ?? defaultOptions.User,
            UseSsl = overrideOptions.UseSsl || defaultOptions.UseSsl,
            UseTls = overrideOptions.UseTls || defaultOptions.UseTls,
            TlsCertificatePath = overrideOptions.TlsCertificatePath ?? defaultOptions.TlsCertificatePath,
            TlsCertificatePassword = overrideOptions.TlsCertificatePassword ?? defaultOptions.TlsCertificatePassword,
            AllowInsecureTls = overrideOptions.AllowInsecureTls || defaultOptions.AllowInsecureTls,
            ConnectTimeoutMilliseconds = overrideOptions.ConnectTimeoutMilliseconds == 5000
                ? defaultOptions.ConnectTimeoutMilliseconds
                : overrideOptions.ConnectTimeoutMilliseconds,
            ConnectRetry = overrideOptions.ConnectRetry == 5
                ? defaultOptions.ConnectRetry
                : overrideOptions.ConnectRetry,
            AbortOnConnectFail = overrideOptions.AbortOnConnectFail || defaultOptions.AbortOnConnectFail,
            SyncTimeoutMilliseconds = overrideOptions.SyncTimeoutMilliseconds == 5000
                ? defaultOptions.SyncTimeoutMilliseconds
                : overrideOptions.SyncTimeoutMilliseconds,
            DatabaseIndex = overrideOptions.DatabaseIndex,
            GlobalKeyPrefix = overrideOptions.GlobalKeyPrefix ?? defaultOptions.GlobalKeyPrefix,
            Password = overrideOptions.Password ?? defaultOptions.Password
        };
    }
}