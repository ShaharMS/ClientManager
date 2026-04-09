using ClientManager.Shared.Logging;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Implementations;

namespace ClientManager.Api.Utils.Extensions;

/// <summary>
/// Registers public-API local services that remain after the storage split.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the public API's local adapter services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    public static IServiceCollection AddClientManager(
        this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

        services.AddScoped<IResourceAllocationService, ResourceAllocationService>();
        services.AddScoped<IAccessControlService, AccessControlService>();

        return services;
    }
}
