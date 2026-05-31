using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Services.InternalClients.Implementations.Configuration;
using ClientManager.Api.Services.InternalClients.Implementations;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Api.Services.InternalClients.Interfaces;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Utils.Extensions;


// CR: This file should be merged into ServiceCollectionExtensions.cs
/// <summary>
/// Registers typed HTTP clients for the internal storage-facing API.
/// </summary>
public static class StorageApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Adds typed internal clients used to call the storage-facing API.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The current host environment.</param>
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