using ClientManager.Api.Models.Configuration;
using ClientManager.Api.Services.InternalClients.Implementations.Configuration;
using ClientManager.Api.Services.InternalClients.Implementations;
using ClientManager.Api.Services.InternalClients.Interfaces.Configuration;
using ClientManager.Api.Services.InternalClients.Interfaces;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Utils.Extensions;

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
        services.AddOptions<StorageApiOptions>()
            .Bind(configuration.GetSection(StorageApiOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                $"{StorageApiOptions.SectionName}:BaseUrl must be an absolute URI.")
            .Validate(options => options.Timeout > TimeSpan.Zero, $"{StorageApiOptions.SectionName}:Timeout must be positive.")
            .Validate(options => options.ReadRetryCount >= 0, $"{StorageApiOptions.SectionName}:ReadRetryCount cannot be negative.")
            .Validate(options => options.InitialRetryDelay >= TimeSpan.Zero, $"{StorageApiOptions.SectionName}:InitialRetryDelay cannot be negative.")
            .Validate(options => options.FailureThreshold > 0, $"{StorageApiOptions.SectionName}:FailureThreshold must be greater than zero.")
            .Validate(options => options.CircuitBreakDuration > TimeSpan.Zero, $"{StorageApiOptions.SectionName}:CircuitBreakDuration must be positive.")
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