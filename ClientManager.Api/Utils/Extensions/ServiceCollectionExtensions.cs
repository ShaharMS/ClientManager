using ClientManager.Api.Services.Implementations;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Logging;

namespace ClientManager.Api.Utils.Extensions;

/// <summary>
/// Registers the public API's dependency-injection surface: the request-scoped adapter services the
/// controllers depend on, which in turn delegate to the in-process storage domain services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the public API's local adapter services and shared logging.
    /// These are the request-scoped services that controllers resolve and which in turn delegate to
    /// the in-process storage domain services.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPublicApiServices(
        this IServiceCollection services)
    {
        services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

        services.AddScoped<IResourceAllocationService, ResourceAllocationService>();
        services.AddScoped<IAccessControlService, AccessControlService>();

        services.AddScoped<IClientConfigurationService, ClientConfigurationService>();
        services.AddScoped<IClientServiceSettingsService, ClientServiceSettingsService>();
        services.AddScoped<IClientResourcePoolSettingsService, ClientResourcePoolSettingsService>();
        services.AddScoped<IClientGlobalRateLimitService, ClientGlobalRateLimitService>();

        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IResourcePoolCatalogService, ResourcePoolCatalogService>();
        services.AddScoped<IGlobalRateLimitCatalogService, GlobalRateLimitCatalogService>();

        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IMetricsService, MetricsService>();

        return services;
    }
}
