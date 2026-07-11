using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
using ClientManager.Api.Services.Storage.Exporters;
using ClientManager.Api.Services.Storage.Extensions;
using ClientManager.Api.Services.Storage.Instrumentation;
using ClientManager.Api.Services.Storage.RateLimiting;
using ClientManager.Api.Services.Storage.RateLimiting.Strategies;
using ClientManager.Api.Services.Storage.UsageTracking;
using ClientManager.Shared.Configuration.Storage;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Registers the in-process storage domain services so the API can run
/// its runtime, read-model, catalog, seeding, and background work directly against
/// <c>ClientManager.DataAccess</c>.
/// </summary>
/// <remarks>
/// <see cref="ClientManager.Shared.Logging.IAppLogger{T}"/> is intentionally not registered here
/// because the API already adds it as part of its public services registration.
/// </remarks>
public static class StorageServicesRegistration
{
    /// <summary>
    /// Adds the in-process storage domain services and their hosted background workers.
    /// </summary>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <param name="configuration">The application configuration used to bind persistence and storage options.</param>
    /// <param name="environment">The hosting environment used to resolve storage provider paths.</param>
    public static IServiceCollection AddInProcessStorageServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var persistenceOptions = configuration
            .GetSection(PersistenceOptions.SectionName)
            .Get<PersistenceOptions>() ?? new PersistenceOptions();

        services.AddSingleton<StorageMetrics>();
        services.AddStorageProviders(persistenceOptions, environment);
        services.AddStorageRepositories();
        services.AddMemoryCache();
        services.AddSingleton<IConfigureOptions<DangerZoneOptions>, DangerZoneOptionsPostConfigure>();
        services.AddSingleton<IValidateOptions<DangerZoneOptions>, DangerZoneOptionsValidator>();
        services.AddOptions<DangerZoneOptions>()
            .Bind(configuration.GetSection(DangerZoneOptions.SectionName))
            .ValidateOnStart();
        var storageReadCacheSection = configuration.GetSection(
            $"{DangerZoneOptions.SectionName}:{DangerZoneOptions.StorageReadCacheSubsection}");
        var storageReadCachePath = $"{DangerZoneOptions.SectionName}:{DangerZoneOptions.StorageReadCacheSubsection}";
        services.AddOptions<StorageReadCacheOptions>()
            .Bind(storageReadCacheSection)
            .Validate(options => options.CatalogTtl > TimeSpan.Zero, $"{storageReadCachePath}:CatalogTtl must be positive.")
            .Validate(options => options.HotPathCatalogTtl > TimeSpan.Zero, $"{storageReadCachePath}:HotPathCatalogTtl must be positive.")
            .Validate(options => options.StatisticsTtl > TimeSpan.Zero, $"{storageReadCachePath}:StatisticsTtl must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IStorageReadCache, StorageReadCache>();
        services.AddSingleton<IValidateOptions<RateLimitingSettings>, RateLimitingSettingsValidator>();
        services.AddOptions<RateLimitingSettings>()
            .Bind(configuration.GetSection(RateLimitingSettings.SectionName))
            .ValidateOnStart();
        RegisterRateLimiting(services);
        RegisterRuntimeServices(services);
        RegisterReadModelServices(services);
        services.AddScoped<IClientConfigurationCatalogService, ClientConfigurationCatalogService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IResourcePoolCatalogService, ResourcePoolCatalogService>();
        services.AddScoped<IGlobalRateLimitCatalogService, GlobalRateLimitCatalogService>();
        services.AddScoped<ISeedCatalogService, SeedCatalogService>();
        services.AddSingleton<SeedOperationGate>();
        RegisterBackgroundServices(services);
        RegisterSeeding(services, configuration, environment);
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
        services.AddScoped<IStatisticsTimeseriesService, StatisticsTimeseriesService>();
        services.AddScoped<IStatisticsPrecomputeService, StatisticsPrecomputeService>();
        services.AddScoped<IPrometheusExportService, PrometheusExportService>();
        services.AddScoped<IGrafanaExportService, GrafanaExportService>();
    }

    private static void RegisterBackgroundServices(IServiceCollection services)
    {
        services.AddHostedService<AllocationCleanupService>();
        services.AddHostedService<UsagePersistenceService>();
    }

    private static void RegisterSeeding(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var seedOptions = configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>();
        if (seedOptions is null)
        {
            return;
        }

        var dangerZone = configuration.GetSection(DangerZoneOptions.SectionName).Get<DangerZoneOptions>() ?? new DangerZoneOptions();
        DangerZoneOptionsPostConfigure.ApplyDefaults(dangerZone, environment);
        if (!dangerZone.IsStartupSeedingEnabled)
        {
            return;
        }

        services.AddSingleton(seedOptions);
        services.AddHostedService<DataSeedService>();
    }
}
