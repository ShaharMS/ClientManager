using ClientManager.DataAccess.Stores.Implementations;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using MongoDB.Driver;
using StackExchange.Redis;
using ClientManager.DataAccess.Repositories.Implementations;
using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Implementations.Exporters;
using ClientManager.Api.Services.Implementations.RateLimiting.Strategies;
using ClientManager.Api.Services.Implementations.RateLimiting;
using ClientManager.Api.Services.Implementations.UsageTracking;
using ClientManager.Api.Services.Implementations;
using System.Security.Cryptography.X509Certificates;

namespace ClientManager.Api.Utils.Extensions;

/// <summary>
/// Extension methods for registering all ClientManager services and persistence providers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all ClientManager services, persistence providers, rate limiting, and background services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    public static IServiceCollection AddClientManager(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

        var persistenceOptions = configuration
            .GetSection(PersistenceOptions.SectionName)
            .Get<PersistenceOptions>() ?? new PersistenceOptions();

        ValidateStorageConfiguration(persistenceOptions);
        RegisterDocumentStore(services, persistenceOptions);
        LogStorageConfiguration(persistenceOptions);
        RegisterRepositories(services);
        RegisterRateLimiting(services);
        RegisterApplicationServices(services);
        RegisterBackgroundServices(services);
        RegisterSeeding(services, configuration);

        services.AddMemoryCache();
        services.Configure<UsageTrackingOptions>(
            configuration.GetSection(UsageTrackingOptions.SectionName));

        return services;
    }

    private static void RegisterDocumentStore(IServiceCollection services, PersistenceOptions options)
    {
        var mongoClients = new Dictionary<string, IMongoClient>();
        var redisMultiplexers = new Dictionary<string, IConnectionMultiplexer>();

        foreach (var role in Enum.GetValues<StorageRole>())
        {
            var binding = ResolveBinding(options, role);
            var store = CreateDocumentStore(binding, mongoClients, redisMultiplexers);
            services.AddKeyedSingleton<IDocumentStore>(role, (_, _) => store);
        }
    }

    private static void ValidateStorageConfiguration(PersistenceOptions options)
    {
        if (!Enum.IsDefined(options.DefaultProvider))
            throw new PersistenceConfigurationException(
                $"Persistence:DefaultProvider has invalid value '{options.DefaultProvider}'.");

        foreach (var role in Enum.GetValues<StorageRole>())
        {
            var binding = ResolveBinding(options, role);

            switch (binding.Provider)
            {
                case PersistenceProvider.MongoDb:
                    var mongo = binding.MongoDb;
                    if (mongo is null || string.IsNullOrWhiteSpace(mongo.ConnectionString))
                        throw new PersistenceConfigurationException(
                            $"Storage configuration invalid: role '{role}' is configured to use MongoDb, " +
                            $"but no MongoDB ConnectionString was provided. Set either " +
                            $"Persistence:Roles:{role}:MongoDb:ConnectionString or Persistence:DefaultMongoDb:ConnectionString.");
                    break;

                case PersistenceProvider.Redis:
                    var redis = binding.Redis;
                    if (redis is null || string.IsNullOrWhiteSpace(redis.ConnectionString))
                        throw new PersistenceConfigurationException(
                            $"Storage configuration invalid: role '{role}' is configured to use Redis, " +
                            $"but no Redis ConnectionString was provided. Set either " +
                            $"Persistence:Roles:{role}:Redis:ConnectionString or Persistence:DefaultRedis:ConnectionString.");
                    break;

                case PersistenceProvider.JsonFile:
                    var json = binding.JsonFile ?? new JsonFileStoreOptions();
                    if (string.IsNullOrWhiteSpace(json.DataDirectory))
                        throw new PersistenceConfigurationException(
                            $"Storage configuration invalid: role '{role}' is configured to use JsonFile, " +
                            $"but DataDirectory is empty. Set either " +
                            $"Persistence:Roles:{role}:JsonFile:DataDirectory or Persistence:DefaultJsonFile:DataDirectory.");
                    break;

                case PersistenceProvider.Lucene:
                    var lucene = binding.Lucene ?? new LuceneStoreOptions();
                    if (string.IsNullOrWhiteSpace(lucene.IndexDirectory))
                        throw new PersistenceConfigurationException(
                            $"Storage configuration invalid: role '{role}' is configured to use Lucene, " +
                            $"but IndexDirectory is empty. Set either " +
                            $"Persistence:Roles:{role}:Lucene:IndexDirectory or Persistence:DefaultLucene:IndexDirectory.");
                    break;
            }
        }
    }

    private static void LogStorageConfiguration(PersistenceOptions options)
    {
        var logger = NLog.LogManager.GetLogger("StorageConfiguration");

        foreach (var role in Enum.GetValues<StorageRole>())
        {
            var binding = ResolveBinding(options, role);
            var detail = binding.Provider switch
            {
                PersistenceProvider.JsonFile => binding.JsonFile?.DataDirectory ?? "./data",
                PersistenceProvider.MongoDb => MaskConnectionString(binding.MongoDb?.ConnectionString),
                PersistenceProvider.Redis => MaskConnectionString(binding.Redis?.ConnectionString),
                PersistenceProvider.Lucene => binding.Lucene?.IndexDirectory ?? "./lucene-index",
                _ => "unknown"
            };

            logger.Info("Storage role {Role} -> {Provider} ({Detail})", role, binding.Provider, detail);
        }
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "(not configured)";

        try
        {
            var uri = new Uri(connectionString);
            return $"{uri.Host}:{uri.Port}";
        }
        catch
        {
            // Not a URI format (e.g. Redis "host:port,password=..." style)
            var parts = connectionString.Split(',');
            return parts.Length > 0 ? parts[0] : "(masked)";
        }
    }

    private static StorageRoleBinding ResolveBinding(PersistenceOptions options, StorageRole role)
    {
        if (options.Roles is not null && options.Roles.TryGetValue(role, out var explicitBinding))
            return explicitBinding;

        return new StorageRoleBinding
        {
            Provider = options.DefaultProvider,
            MongoDb = options.DefaultMongoDb,
            Redis = options.DefaultRedis,
            JsonFile = options.DefaultJsonFile,
            Lucene = options.DefaultLucene
        };
    }

    private static IDocumentStore CreateDocumentStore(
        StorageRoleBinding binding,
        Dictionary<string, IMongoClient> mongoClients,
        Dictionary<string, IConnectionMultiplexer> redisMultiplexers)
    {
        return binding.Provider switch
        {
            PersistenceProvider.JsonFile => CreateJsonFileStore(binding.JsonFile),
            PersistenceProvider.MongoDb => CreateMongoStore(binding.MongoDb, mongoClients),
            PersistenceProvider.Redis => CreateRedisStore(binding.Redis, redisMultiplexers),
            PersistenceProvider.Lucene => CreateLuceneStore(binding.Lucene),
            _ => throw new PersistenceConfigurationException(
                $"Unsupported persistence provider: {binding.Provider}")
        };
    }

    private static JsonFileDocumentStore CreateJsonFileStore(JsonFileStoreOptions? options)
    {
        var resolved = options ?? new JsonFileStoreOptions();
        return new JsonFileDocumentStore(resolved.DataDirectory);
    }

    private static MongoDBDocumentStore CreateMongoStore(
        MongoDbStoreOptions? options,
        Dictionary<string, IMongoClient> clientCache)
    {
        if (options is null)
            throw new PersistenceConfigurationException(
                "MongoDb settings are required for roles using the MongoDb provider.");

        if (!clientCache.TryGetValue(options.ConnectionString, out var client))
        {
            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
            settings.ConnectTimeout = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds);
            settings.MaxConnectionPoolSize = options.MaxConnectionPoolSize;
            settings.RetryWrites = options.RetryWrites;

            if (options.UseTls)
            {
                settings.UseTls = true;
                settings.SslSettings ??= new SslSettings();

                if (options.TlsCertificatePath is not null)
                {
                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                        options.TlsCertificatePath, options.TlsCertificatePassword);
                    settings.SslSettings.ClientCertificates = [certificate];
                }
            }

            if (options.AllowInsecureTls)
                settings.AllowInsecureTls = true;

            if (options.AuthenticationMechanism is not null)
            {
                settings.Credential = settings.Credential?.WithMechanismProperty(
                    "MECHANISM", options.AuthenticationMechanism);
            }

            client = new MongoClient(settings);
            clientCache[options.ConnectionString] = client;
        }

        var database = client.GetDatabase(options.DatabaseName);
        return new MongoDBDocumentStore(database);
    }

    private static RedisDocumentStore CreateRedisStore(
        RedisStoreOptions? options,
        Dictionary<string, IConnectionMultiplexer> multiplexerCache)
    {
        if (options is null)
            throw new PersistenceConfigurationException(
                "Redis settings are required for roles using the Redis provider.");

        if (!multiplexerCache.TryGetValue(options.ConnectionString, out var multiplexer))
        {
            var config = ConfigurationOptions.Parse(options.ConnectionString);
            config.ConnectTimeout = options.ConnectTimeoutMilliseconds;
            config.SyncTimeout = options.SyncTimeoutMilliseconds;
            config.DefaultDatabase = options.DatabaseIndex;

            if (options.Password is not null)
                config.Password = options.Password;

            if (options.UseTls)
            {
                config.Ssl = true;

                if (options.TlsCertificatePath is not null)
                {
                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                        options.TlsCertificatePath, options.TlsCertificatePassword);
                    config.CertificateSelection += (_, _, _, _, _) => certificate;
                }
            }

            if (options.AllowInsecureTls)
                config.CertificateValidation += (_, _, _, _) => true;

            multiplexer = ConnectionMultiplexer.Connect(config);
            multiplexerCache[options.ConnectionString] = multiplexer;
        }

        return new RedisDocumentStore(multiplexer);
    }

    private static LuceneDocumentStore CreateLuceneStore(LuceneStoreOptions? options)
    {
        var resolved = options ?? new LuceneStoreOptions();
        return new LuceneDocumentStore(resolved.IndexDirectory);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddSingleton<IClientConfigurationDatabase>(sp =>
            new ClientConfigurationDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration)));
        services.AddSingleton<IEntityRepository<Service>>(sp =>
            new EntityRepository<Service>(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration), "services", s => s.Id));
        services.AddSingleton<IEntityRepository<ResourcePool>>(sp =>
            new EntityRepository<ResourcePool>(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration), "resource_pools", r => r.Id));
        services.AddSingleton<IGlobalRateLimitDatabase>(sp =>
            new GlobalRateLimitDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Configuration)));
        services.AddSingleton<IRateLimitStateDatabase>(sp =>
            new RateLimitStateDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.RateLimiting)));
        services.AddSingleton<IResourceAllocationDatabase>(sp =>
            new ResourceAllocationDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Allocations)));
        services.AddSingleton<IUsageSnapshotDatabase>(sp =>
            new UsageSnapshotDatabase(
                sp.GetRequiredKeyedService<IDocumentStore>(StorageRole.Statistics),
                sp.GetRequiredService<IClientConfigurationDatabase>()));
    }

    private static void RegisterRateLimiting(IServiceCollection services)
    {
        services.AddSingleton<FixedWindowStrategy>();
        services.AddSingleton<ApproximateSlidingWindowStrategy>();
        services.AddSingleton<TokenBucketStrategy>();
        services.AddSingleton<RateLimitStrategyResolver>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IRateLimitService, RateLimitService>();
        services.AddScoped<IResourceAllocationService, ResourceAllocationService>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IPrometheusExportService, PrometheusExportService>();
        services.AddScoped<IGrafanaExportService, GrafanaExportService>();
        services.AddSingleton<UsageBuffer>();
        services.AddSingleton<IUsageRecorder, UsageRecorder>();
    }

    private static void RegisterBackgroundServices(IServiceCollection services)
    {
        services.AddHostedService<AllocationCleanupService>();
        services.AddHostedService<UsagePersistenceService>();
    }

    private static void RegisterSeeding(IServiceCollection services, IConfiguration configuration)
    {
        var seedOptions = configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>();
        if (seedOptions is not null)
        {
            services.AddSingleton(seedOptions);
            services.AddHostedService<DataSeedService>();
        }
    }
}
