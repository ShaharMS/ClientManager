using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Services.Implementations;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Internal.Implementations;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Api.Utils.StorageApi;
using ClientManager.Shared.Logging;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Utils.Extensions;

/// <summary>
/// Registers the public API's dependency-injection surface: the request-scoped adapter services the
/// controllers depend on directly, and the typed HTTP clients those services use to reach the
/// internal storage-facing API.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the public API's local adapter services and shared logging.
    /// These are the request-scoped services that controllers resolve and which in turn delegate to
    /// the internal storage clients.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPublicApiServices(
        this IServiceCollection services)
    {
        services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

        services.AddScoped<IResourceAllocationService, ResourceAllocationService>();
        services.AddScoped<IAccessControlService, AccessControlService>();

        return services;
    }

    /// <summary>
    /// Registers the typed HTTP clients, resilience pipeline, and bound options used to call the
    /// internal storage-facing API.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The application configuration providing the storage API section.</param>
    /// <param name="environment">The current host environment, used to relax TLS validation in development.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddStorageApiClients(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<IValidateOptions<StorageApiOptions>, StorageApiOptionsValidator>();
        services.AddOptions<StorageApiOptions>()
            .Bind(configuration.GetSection(StorageApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<StorageApiResilienceState>();
        services.AddTransient<StorageApiResilienceHandler>();

        services.AddHttpClient<IClientConfigurationStoreClient, ClientConfigurationStoreClient>(ConfigureClient)
            .AddHttpMessageHandler<StorageApiResilienceHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(environment));

        services.AddHttpClient<IServiceCatalogClient, ServiceCatalogClient>(ConfigureClient)
            .AddHttpMessageHandler<StorageApiResilienceHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(environment));

        services.AddHttpClient<IResourcePoolCatalogClient, ResourcePoolCatalogClient>(ConfigureClient)
            .AddHttpMessageHandler<StorageApiResilienceHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(environment));

        services.AddHttpClient<IGlobalRateLimitCatalogClient, GlobalRateLimitCatalogClient>(ConfigureClient)
            .AddHttpMessageHandler<StorageApiResilienceHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(environment));

        services.AddHttpClient<IRuntimeStateClient, RuntimeStateClient>(ConfigureClient)
            .AddHttpMessageHandler<StorageApiResilienceHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(environment));

        services.AddHttpClient<IStatisticsReadClient, StatisticsReadClient>(ConfigureClient)
            .AddHttpMessageHandler<StorageApiResilienceHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(environment));

        return services;
    }

    private static void ConfigureClient(IServiceProvider serviceProvider, HttpClient httpClient)
    {
        var options = serviceProvider.GetRequiredService<IOptions<StorageApiOptions>>().Value;

        httpClient.BaseAddress = new Uri(options.BaseUrl);
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    private static HttpMessageHandler CreateHandler(IHostEnvironment environment)
    {
        var handler = new HttpClientHandler();
        if (environment.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }
}
