using ClientManager.Shared.Logging;
using ClientManager.StorageApi.Models.Configuration;
using ClientManager.StorageApi.Services.Implementations;
using ClientManager.StorageApi.Services.Implementations.Exporters;
using ClientManager.StorageApi.Services.Implementations.RateLimiting;
using ClientManager.StorageApi.Services.Implementations.RateLimiting.Strategies;
using ClientManager.StorageApi.Services.Implementations.UsageTracking;
using ClientManager.StorageApi.Services.Interfaces;
using ClientManager.Shared.Configuration.Storage;

namespace ClientManager.StorageApi.Utils.Extensions;

/// <summary>
/// Registers storage host dependencies and infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the storage API's shared infrastructure registrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    public static IServiceCollection AddStorageApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

        var persistenceOptions = configuration
            .GetSection(PersistenceOptions.SectionName)
            .Get<PersistenceOptions>() ?? new PersistenceOptions();

        services.AddStorageProviders(persistenceOptions);
        services.AddStorageRepositories();
        services.AddMemoryCache();
        services.AddOptions<StorageReadCacheOptions>()
            .Bind(configuration.GetSection(StorageReadCacheOptions.SectionName))
            .Validate(options => options.CatalogTtl > TimeSpan.Zero, "StorageReadCache:CatalogTtl must be positive.")
            .Validate(options => options.StatisticsTtl > TimeSpan.Zero, "StorageReadCache:StatisticsTtl must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IStorageReadCache, StorageReadCache>();
        RegisterRateLimiting(services);
        RegisterRuntimeServices(services);
        RegisterReadModelServices(services);
        services.AddScoped<IClientConfigurationCatalogService, ClientConfigurationCatalogService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IResourcePoolCatalogService, ResourcePoolCatalogService>();
        services.AddScoped<IGlobalRateLimitCatalogService, GlobalRateLimitCatalogService>();
        RegisterBackgroundServices(services);
        RegisterSeeding(services, configuration);
        services.Configure<UsageTrackingOptions>(
            configuration.GetSection(UsageTrackingOptions.SectionName));

        return services;
    }

    private static void RegisterRateLimiting(IServiceCollection services)
    {
        services.AddSingleton<FixedWindowStrategy>();
        services.AddSingleton<ApproximateSlidingWindowStrategy>();
        services.AddSingleton<TokenBucketStrategy>();
        services.AddSingleton<RateLimitStrategyResolver>();
    }

    private static void RegisterRuntimeServices(IServiceCollection services)
    {
        services.AddScoped<IRateLimitService, RateLimitService>();
        services.AddScoped<IResourceAllocationService, ResourceAllocationService>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddSingleton<UsageBuffer>();
        services.AddSingleton<IUsageRecorder, UsageRecorder>();
    }

    private static void RegisterReadModelServices(IServiceCollection services)
    {
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IPrometheusExportService, PrometheusExportService>();
        services.AddScoped<IGrafanaExportService, GrafanaExportService>();
    }

    private static void RegisterBackgroundServices(IServiceCollection services)
    {
        services.AddHostedService<AllocationCleanupService>();
        services.AddHostedService<UsagePersistenceService>();
    }

    private static void RegisterSeeding(IServiceCollection services, IConfiguration configuration)
    {
        var seedOptions = configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>();
        if (seedOptions is null)
        {
            return;
        }

        services.AddSingleton(seedOptions);
        services.AddHostedService<DataSeedService>();
    }
}