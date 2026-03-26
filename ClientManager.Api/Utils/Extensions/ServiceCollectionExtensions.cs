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
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Implementations.Exporters;
using ClientManager.Api.Services.Implementations.RateLimiting.Strategies;
using ClientManager.Api.Services.Implementations.RateLimiting;
using ClientManager.Api.Services.Implementations.UsageTracking;
using ClientManager.Api.Services.Implementations;

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

        RegisterDocumentStore(services, persistenceOptions);
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
        switch (options.Provider)
        {
            case PersistenceProvider.JsonFile:
                services.AddSingleton<IDocumentStore>(
                    new JsonFileDocumentStore(options.JsonFileDataDirectory));
                break;

            case PersistenceProvider.MongoDb:
                services.AddSingleton<IMongoClient>(
                    new MongoClient(options.MongoDbConnectionString));
                services.AddSingleton<IMongoDatabase>(sp =>
                    sp.GetRequiredService<IMongoClient>().GetDatabase(options.MongoDbDatabaseName));
                services.AddSingleton<IDocumentStore, MongoDBDocumentStore>();
                break;

            case PersistenceProvider.Redis:
                services.AddSingleton<IConnectionMultiplexer>(
                    ConnectionMultiplexer.Connect(options.RedisConnectionString!));
                services.AddSingleton<IDocumentStore, RedisDocumentStore>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported persistence provider: {options.Provider}");
        }
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddSingleton<IClientConfigurationDatabase, ClientConfigurationDatabase>();
        services.AddSingleton<IEntityRepository<Service>>(sp =>
            new EntityRepository<Service>(sp.GetRequiredService<IDocumentStore>(), "services", s => s.Id));
        services.AddSingleton<IEntityRepository<ResourcePool>>(sp =>
            new EntityRepository<ResourcePool>(sp.GetRequiredService<IDocumentStore>(), "resource_pools", r => r.Id));
        services.AddSingleton<IGlobalRateLimitDatabase, GlobalRateLimitDatabase>();
        services.AddSingleton<IRateLimitStateDatabase, RateLimitStateDatabase>();
        services.AddSingleton<IResourceAllocationDatabase, ResourceAllocationDatabase>();
        services.AddSingleton<IUsageSnapshotDatabase, UsageSnapshotDatabase>();
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
