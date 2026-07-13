using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Services;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Extensions;
using ClientManager.Api.Services.Storage.Instrumentation;
using ClientManager.Api.Services.Storage.RateLimiting;
using ClientManager.Api.Services.Storage.RateLimiting.Strategies;
using ClientManager.Shared.Configuration.Storage;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Registers the lean in-process storage and runtime services.
/// </summary>
public static class StorageServicesRegistration
{
    /// <summary>
    /// Adds storage providers, repositories, caching, rate limiting, seed services, and RPM accounting.
    /// </summary>
    public static IServiceCollection AddInProcessStorageServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var persistenceOptions = configuration
            .GetSection(PersistenceOptions.SectionName)
            .Get<PersistenceOptions>() ?? new PersistenceOptions();
        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<StorageMetrics>();
        services.AddStorageProviders(persistenceOptions, environment);
        services.AddStorageRepositories();
        services.AddMemoryCache();

        services.AddOptions<StorageReadCacheOptions>()
            .Bind(configuration.GetSection(StorageReadCacheOptions.SectionName))
            .Validate(options => options.CatalogTtl > TimeSpan.Zero, $"{StorageReadCacheOptions.SectionName}:CatalogTtl must be positive.")
            .Validate(options => options.HotPathCatalogTtl > TimeSpan.Zero, $"{StorageReadCacheOptions.SectionName}:HotPathCatalogTtl must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IStorageReadCache, StorageReadCache>();

        services.AddSingleton<IValidateOptions<RateLimitingSettings>, RateLimitingSettingsValidator>();
        services.AddOptions<RateLimitingSettings>()
            .Bind(configuration.GetSection(RateLimitingSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<RpmOptions>, RpmOptionsValidator>();
        services.AddOptions<RpmOptions>()
            .Bind(configuration.GetSection(RpmOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<RpmAccountingService>();

        services.AddSingleton<FixedWindowStrategy>();
        services.AddSingleton<ApproximateSlidingWindowStrategy>();
        services.AddSingleton<TokenBucketStrategy>();
        services.AddSingleton<RateLimitStrategyResolver>();

        services.AddScoped<RateLimitService>();
        services.AddScoped<IRateLimitService>(sp => sp.GetRequiredService<RateLimitService>());
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddScoped<IClientConfigurationCatalogService, ClientConfigurationCatalogService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IGlobalRateLimitCatalogService, GlobalRateLimitCatalogService>();
        services.AddScoped<ISeedCatalogService, SeedCatalogService>();
        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<SeedOperationGate>();

        // ponytail: keep explicit registration local until a second composition root needs reuse.
        services.AddScoped<IStatisticsService, StatisticsService>();
        return services;
    }
}
