using ClientManager.Shared.Logging;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Implementations;

namespace ClientManager.Api.Utils.Extensions;

// CR: Update documentation for this class and its methods, it doesnt make much sense and doesnt expalin really anything
/// <summary>
/// Registers public-API local services that remain after the storage split.
/// </summary>
public static class ServiceCollectionExtensions
{
    // CR: Bad naming and bad documentation, this method is not really adding a client manager, its adding services related to the client manager. rename and explain more appropriately.
    /// <summary>
    /// Registers the public API's local adapter services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddClientManager(
        this IServiceCollection services)
    {
        services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

        services.AddScoped<IResourceAllocationService, ResourceAllocationService>();
        services.AddScoped<IAccessControlService, AccessControlService>();

        return services;
    }
}
