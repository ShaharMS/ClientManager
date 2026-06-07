using ClientManager.Api.Services.Implementations;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Shared.Logging;

namespace ClientManager.Api.Utils.Extensions;

/// <summary>
/// Registers the public API's dependency-injection surface.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers shared logging and the public statistics read-model composer.
    /// </summary>
    public static IServiceCollection AddPublicApiServices(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));
        services.AddScoped<IStatisticsService, StatisticsService>();
        return services;
    }
}
